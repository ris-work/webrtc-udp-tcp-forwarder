#![allow(non_snake_case)]
#![allow(unused_parens)]
#![allow(unused_assignments)]
#![allow(non_camel_case_types)]
#![allow(dead_code)]
#![allow(unused_mut)]
#![allow(unused_variables)]
#![allow(non_upper_case_globals)]
use anyhow::Result;
use base64::engine::general_purpose;
use base64::Engine;
use bytes::Bytes;
use compile_time_run::run_command_str;
use futures::executor::block_on;
use lazy_static::lazy_static;
use log::{debug, error, info, trace, warn};
use parking_lot::Mutex;
use serde::Deserialize;
use std::env;
use std::error;
use std::time::Duration;

use crossbeam_channel::{bounded, Receiver, Sender};
use std::error::Error;
use std::fs::read_to_string;
use std::io;
use std::io::ErrorKind;
use std::io::Read;
use std::io::Write;
use std::io::{BufRead, BufReader};
use std::net::TcpListener;
use std::net::TcpStream;
use std::net::UdpSocket;
#[cfg(unix)]
use std::os::unix::net::{UnixListener, UnixStream};
use std::process::exit;
use std::sync::atomic::Ordering;
use std::sync::atomic::{AtomicBool, AtomicU64};
use std::sync::Arc;
use std::thread;
use std::time;
use tokio::runtime::Builder;
use tokio::runtime::Runtime;
use tokio::sync::Semaphore;

#[cfg(windows)]
use uds_windows::{UnixListener, UnixStream};
use webrtc::api::interceptor_registry::register_default_interceptors;
use webrtc::api::media_engine::MediaEngine;
use webrtc::api::APIBuilder;
use webrtc::data_channel::data_channel_init::RTCDataChannelInit;
use webrtc::data_channel::data_channel_message::DataChannelMessage;
use webrtc::data_channel::RTCDataChannel;
use webrtc::ice_transport::ice_credential_type::RTCIceCredentialType;
use webrtc::ice_transport::ice_server::RTCIceServer;
use webrtc::interceptor::registry::Registry;
use webrtc::peer_connection::configuration::RTCConfiguration;

use webrtc::peer_connection::peer_connection_state::RTCPeerConnectionState;
use webrtc::peer_connection::sdp::session_description::RTCSessionDescription;
use webrtc::peer_connection::RTCPeerConnection;

use webrtc_udp_forwarder::hmac::{ConstructAuthenticatedMessage, HashAuthenticatedMessage, VerifyAndReturn};
use webrtc_udp_forwarder::message::{CheckAndReturn, ConstructMessage, TimedMessage};
use webrtc_udp_forwarder::AlignedMessage::AlignedMessage;
use webrtc_udp_forwarder::Config;
use webrtc_udp_forwarder::Pinning;

use websocket::header::{Authorization, Basic, Bearer, Headers};
use websocket::OwnedMessage::Text;
use websocket::{ClientBuilder, Message};

use crossbeam_queue::ArrayQueue;

use mimalloc::MiMalloc;

#[global_allocator]
static GLOBAL: MiMalloc = MiMalloc;

static STREAM_LAST_ACTIVE_TIME: AtomicU64 = AtomicU64::new(0);
static OtherSocketReady: AtomicBool = AtomicBool::new(false);
static DataChannelReady: AtomicBool = AtomicBool::new(false);
static CAN_RECV: AtomicBool = AtomicBool::new(true);
static MaxOtherSocketSendBufSize: usize = 2048;
static THREAD_STACK_SIZE: usize = 204800;
pub const PKT_SIZE: usize = 2040;

lazy_static! {
    static ref OtherSocketSendBuf: Mutex<Vec<u8>> = Mutex::new(Vec::new());
    static ref SocketSendQueue: ArrayQueue<AlignedMessage> = ArrayQueue::<AlignedMessage>::new(10);
}

#[derive(Deserialize)]
struct WebRTC_Status {
    Status: String,
    SDP_Params: Option<String>,
    SRTP_Params: Option<String>,
}
pub fn encode(b: &str) -> String {
    general_purpose::STANDARD.encode(b)
}
pub fn decode(s: &str) -> Result<String> {
    let b = general_purpose::STANDARD.decode(s)?;
    debug! {"Base64 to byte buffer: OK"};
    let s = String::from_utf8(b)?;
    debug! {"Base64 decoded: {}", s};
    Ok(s)
}
fn handle_TCP_client(stream: TcpStream) {}
async fn create_WebRTC_offer(
    config: &Config,
) -> Result<
    (
        Arc<RTCDataChannel>,
        Arc<RTCPeerConnection>,
        Option<RTCSessionDescription>,
        tokio::sync::mpsc::Receiver<()>,
        tokio::sync::mpsc::Sender<()>,
        crossbeam_channel::Receiver<bool>,
        crossbeam_channel::Sender<bool>,
    ),
    Box<dyn error::Error>,
> {
    // Create a MediaEngine object to configure the supported codec
    let mut m = MediaEngine::default();
    let (cb_done_tx, cb_done_rx): (Sender<bool>, Receiver<bool>) = bounded::<bool>(4);

    // Register default codecs
    m.register_default_codecs().expect("Could not register the default codecs.");

    // Create a InterceptorRegistry. This is the user configurable RTP/RTCP Pipeline.
    // This provides NACKs, RTCP Reports and other features. If you use `webrtc.NewPeerConnection`
    // this is enabled by default. If you are manually managing You MUST create a InterceptorRegistry
    // for each PeerConnection.
    let mut registry = Registry::new();

    // Use the default set of Interceptors
    registry = register_default_interceptors(registry, &mut m).expect("Could not register the interceptor!");

    // Create the API object with the MediaEngine
    let api = APIBuilder::new().with_media_engine(m).with_interceptor_registry(registry).build();

    // Prepare the configuration
    let mut ice_servers: Vec<RTCIceServer> = vec![];
    for ICEServer in config.ICEServers.iter() {
        match (&ICEServer.Username) {
            Some(Username) => ice_servers.push(RTCIceServer {
                urls: ICEServer.URLs.clone(),
                username: Username.clone(),
                credential: ICEServer.Credential.clone().expect("Empty credentials are not allowed."),
                credential_type: RTCIceCredentialType::Password,
                ..Default::default()
            }),
            None => ice_servers.push(RTCIceServer {
                urls: ICEServer.URLs.clone(),
                ..Default::default()
            }),
        }
    }
    let config = RTCConfiguration {
        ice_servers: ice_servers,
        ..Default::default()
    };

    // Create a new RTCPeerConnection
    let peer_connection = Arc::new(api.new_peer_connection(config).await?);

    // Create a datachannel with label 'data'
    //let data_channel = peer_connection.create_data_channel("data", None).await?;
    let data_channel = peer_connection
        .create_data_channel(
            "data",
            Some(RTCDataChannelInit {
                ordered: Some(false),
                max_packet_life_time: Some(0),
                max_retransmits: Some(0),
                protocol: Some("raw".to_string()),
                negotiated: None,
            }),
        )
        .await?;

    let (done_tx, mut done_rx) = tokio::sync::mpsc::channel::<()>(1);
    let (cb_done_tx_2, done_tx_2) = (cb_done_tx.clone(), done_tx.clone());

    // Set the handler for Peer connection state
    // This will notify you when the peer has connected/disconnected
    peer_connection.on_peer_connection_state_change(Box::new(move |s: RTCPeerConnectionState| {
        info!("Peer Connection State has changed: {s}");

        if s == RTCPeerConnectionState::Failed {
            // Wait until PeerConnection has had no network activity for 30 seconds or another failure. It may be reconnected using an ICE Restart.
            // Use webrtc.PeerConnectionStateDisconnected if you are interested in detecting faster timeout.
            // Note that the PeerConnection may come back from PeerConnectionStateDisconnected.
            info!("Peer Connection has gone to failed exiting");
            //std::process::exit(0);
            cb_done_tx.send(true);
            cb_done_tx.send(true);
            cb_done_tx.send(true);
            cb_done_tx.send(true);
            let _ = done_tx.try_send(());
        } else if s == RTCPeerConnectionState::Disconnected {
            info!("Peer Connection has disconnected");
            //std::process::exit(0);
            cb_done_tx.send(true);
            cb_done_tx.send(true);
            cb_done_tx.send(true);
            cb_done_tx.send(true);
            let _ = done_tx.try_send(());
        }

        Box::pin(async {})
    }));

    // Create an offer to send to the browser
    let offer = peer_connection.create_offer(None).await?;

    // Create channel that is blocked until ICE Gathering is complete
    let mut gather_complete = peer_connection.gathering_complete_promise().await;

    // Sets the LocalDescription, and starts our UDP listeners
    peer_connection.set_local_description(offer).await?;

    // Block until ICE Gathering is complete, disabling trickle ICE
    // we do this because we only can exchange one signaling message
    // in a production application you should exchange ICE Candidates via OnICECandidate
    let _ = gather_complete.recv().await;

    let local_description: Option<RTCSessionDescription>;
    // Output the answer in base64 so we can paste it in browser
    if let Some(local_desc) = peer_connection.local_description().await {
        local_description = Some(local_desc.clone());
        let json_str = serde_json::to_string(&local_desc)?;
        //let b64 = encode(&json_str);
        info!("{json_str}");
        let b64 = encode(&json_str);
        info!("{b64}");
    } else {
        info!("generate local_description failed!");
        local_description = None;
    }
    Ok((Arc::clone(&data_channel), peer_connection, local_description, done_rx, done_tx_2, cb_done_rx, cb_done_tx_2))
}
async fn configure_send_receive_udp(
    RTCDC: Arc<RTCDataChannel>,
    OtherSocket: UdpSocket,
    mut done_rx: tokio::sync::mpsc::Receiver<()>,
    mut done_tx: tokio::sync::mpsc::Sender<()>,
    Done_rx: crossbeam_channel::Receiver<bool>,
    cb_done_tx: crossbeam_channel::Sender<bool>,
    config: Config,
) -> (Arc<RTCDataChannel>, UdpSocket) /*, Box<dyn error::Error>>*/
{
    // Register channel opening handling
    info! {"Configuring UDP<=>RTCDC..."};
    let (OtherSocketSendQueue_tx, OtherSocketSendQueue_rx): (Sender<AlignedMessage>, Receiver<AlignedMessage>) = bounded::<AlignedMessage>(128);
    let (WebRTCSendQueue_tx, WebRTCSendQueue_rx): (Sender<AlignedMessage>, Receiver<AlignedMessage>) = bounded::<AlignedMessage>(128);
    let d1 = Arc::clone(&RTCDC);
    let mut ClonedSocketRecv = OtherSocket.try_clone().expect("Unable to clone the UDP socket. :(");
    let mut ClonedSocketSend = OtherSocket.try_clone().expect("Unable to clone the UDP socket. :(");
    let (OtherSocketSendQueue_tx_c, WebRTCSendQueue_tx_c) = (OtherSocketSendQueue_tx.clone(), WebRTCSendQueue_tx.clone());
    let Done_rx_2 = Done_rx.clone();
    let Done_rx_3 = Done_rx.clone();
    let cb_done_tx2 = cb_done_tx.clone();
    let done_tx2 = done_tx.clone();

    let config2 = config.clone();
    let config3 = config.clone();
    let config4 = config.clone();

    RTCDC.on_open(Box::new(move || {
        info!("Data channel '{}'-'{}' open.", d1.label(), d1.id());
        let d2 = Arc::clone(&d1);
        let Done_rx_2 = Done_rx.clone();
        let cu_udp_to_wrtc_sq = move || {
            Pinning::Try(config3.PinnedCores, 2);
            let Done_rx = Done_rx.clone();
            let no_data_count_max: u64 = config.TimeoutCountMax.unwrap_or(3 as u64);
            let mut no_data_counter: u64 = 0;
            move || loop {
                let E_TIMEDOUT = std::io::Error::from(ErrorKind::TimedOut);
                let E_WOULDBLOCK = std::io::Error::from(ErrorKind::WouldBlock);
                if (no_data_counter > no_data_count_max) {
                    let mut i = 0;
                    while (i < 4) {
                        cb_done_tx.try_send(true);
                        done_tx.try_send(());
                        i += 1;
                    }
                    info! {"Quitting due to inactivity... (No datagram has been received.)"};
                    break;
                }
                let mut buf = [0; PKT_SIZE];
                if (Done_rx.try_recv() == Ok(true)) {
                    break;
                }
                match (ClonedSocketRecv.recv(&mut buf)) {
                    Ok(amt) => {
                        no_data_counter = 0;
                        trace! {"{:?}", &buf[0..amt]};
                        debug! {"Enqueued..."};
                        let _ = WebRTCSendQueue_tx.try_send(AlignedMessage { size: amt, data: buf.into() });
                    }
                    Err(E) => match (E.kind()) {
                        std::io::ErrorKind::WouldBlock => {
                            trace!("Unable to read or save to the buffer: {:?}", E);
                            trace! {"Restarting the loop due to previous error: OtherSocket (read) => DataChannel (write)"};
                        }
                        std::io::ErrorKind::TimedOut => {
                            trace!("Unable to read or save to the buffer: {:?}", E);
                            trace! {"Restarting the loop due to previous error: OtherSocket (read) => DataChannel (write)"};
                        }
                        _ => {
                            warn!("Unable to read or save to the buffer: {:?}", E);
                            OtherSocketReady.store(false, Ordering::Relaxed);
                            info! {"Ending the loop due to previous error: OtherSocket (read) => DataChannel (write)"};
                            break;
                        }
                    },
                }
            };
            info! {"[UDP -> WRTC SQ] Exiting concurrency unit (gracefully)..."};
        };
        let udp_to_wrtc_sq = thread::Builder::new()
            .stack_size(THREAD_STACK_SIZE)
            .name("OS->DC".to_string())
            .spawn(cu_udp_to_wrtc_sq)
            .expect("UDP -> WRTC SQ");
        let cu_wrtc_sq_to_wrtc = move || {
            info! {"Spawned the thread: OtherSocket (read) => DataChannel (write)"};
            let rt = Builder::new_multi_thread()
                .worker_threads(1)
                .thread_stack_size(THREAD_STACK_SIZE)
                .on_thread_start(move || Pinning::Try(config2.PinnedCores, 1))
                .thread_name("TOKIO: OS->DC")
                .build()
                .unwrap();
            loop {
                if let Ok(MessageWithSize) = WebRTCSendQueue_rx.recv_timeout(Duration::from_secs(1)) {
                    let buf = MessageWithSize.data;
                    let amt = MessageWithSize.size;
                    trace! {"{:?}", &buf[0..amt]};
                    debug! {"Blocking on DC send"};
                    rt.block_on({
                        let d1 = d1.clone();
                        let d2 = d2.clone();
                        async move {
                            let written_bytes = (d2.send(&Bytes::copy_from_slice(&buf[0..amt]))).await;
                            match (written_bytes) {
                                Ok(Bytes) => {
                                    debug! {"OS->DC: Written {Bytes} bytes!"};
                                }
                                Err(E) => {
                                    warn! {"DataConnection {}: unable to send: {:?}.", d1.label(), E};
                                    DataChannelReady.store(false, Ordering::Relaxed);
                                    info! {"Breaking the loop due to previous error: OtherSocket (read) => DataChannel (write)"};
                                    //break;
                                }
                            }
                        }
                    });
                } else {
                    if Done_rx_2.try_recv() == Ok(true) {
                        break;
                    }
                }
            }
            info! {"[WRTC SQ -> WRTC] Exiting concurrency unit (gracefully)..."};
        };
        let wrtc_sq_to_wrtc = thread::Builder::new()
            .stack_size(THREAD_STACK_SIZE)
            .name("OS->DC".to_string())
            .spawn(cu_wrtc_sq_to_wrtc)
            .expect("Unable to spawn thread: WRTC Q -> WRTC DC");
        #[cfg(feature = "queuemon")]
        let QueueMon = thread::Builder::new().name("QueueMon".to_string()).spawn(move || {
            debug! {"Inactivity monitoring watchdog has started"}
            loop {
                let five_seconds = time::Duration::from_secs(10);
                info! {"WebRTC SendQueue: {}, OtherSocketSendQueue: {}.", WebRTCSendQueue_tx_c.len(), OtherSocketSendQueue_tx_c.len()};
                debug! {"QueueMon will sleep for five seconds."};
                thread::sleep(five_seconds);
                debug! {"Watchdog: Resuming..."};
            }
        });

        Box::pin(async move {})
    }));

    let cu_udp_sq_to_udp = move || {
        Pinning::Try(config4.PinnedCores, 3);
        let no_data_count_max: u64 = config.TimeoutCountMax.unwrap_or(3 as u64);
        let mut no_data_counter: u64 = 0;
        loop {
            debug! {"Blocking on dequeueing UDP send queue. Queue size: {}.", OtherSocketSendQueue_rx.len()};
            let recv_attempt = OtherSocketSendQueue_rx.recv_timeout(time::Duration::from_secs(1));
            debug! {"Block on UDP send queue dequeue is over"};
            match (recv_attempt) {
                Ok(msg) => {
                    let msg = msg.data;
                    no_data_counter = 0;
                    let (mut ClonedSocketSend) = (ClonedSocketSend.try_clone().expect(""));
                    log::debug! {"Message from the UDP send queue: {msg:?}"};
                    match (ClonedSocketSend.send(&msg)) {
                        Ok(amt) => {
                            debug! {"DC->OS: Written {} bytes.", amt};
                            //ClonedSocketSend.flush().expect("Unable to flush the stream.");
                        }
                        #[cold]
                        Err(E) => {
                            warn!("OtherSocket: Unable to write data.");
                            OtherSocketReady.store(false, Ordering::Relaxed);
                            cb_done_tx2.try_send(true);
                            done_tx2.try_send(());
                            //block_on(d.close());
                        }
                    }
                }
                Err(RecvTimeoutError) => {
                    no_data_counter = no_data_counter + 1;
                    if (no_data_counter > no_data_count_max) {
                        let mut i = 0;
                        while (i < 4) {
                            cb_done_tx2.try_send(true);
                            done_tx2.try_send(());
                            i += 1;
                        }
                        info! {"Quitting due to inactivity... (No data channel message has been received.)"};
                        break;
                    }
                    if (Done_rx_3.try_recv() == Ok(true)) {
                        break;
                    }
                }
            }
        }
        info! {"[UDP SQ -> UDP] Exiting concurrency unit (gracefully)..."};
    };
    thread::Builder::new()
        .stack_size(THREAD_STACK_SIZE)
        .name("OS->DC".to_string())
        .spawn(cu_udp_sq_to_udp)
        .expect("Unable to spawn thread: SQ -> UDP");
    // Register text message handling
    let d_label = RTCDC.label().to_owned();
    RTCDC.on_message(Box::new(move |msg: DataChannelMessage| {
        let msg = msg.data.to_vec();
        trace!("Message from DataChannel '{d_label}': '{msg:?}'");
        OtherSocketSendQueue_tx.try_send(AlignedMessage { size: 0, data: msg });

        Box::pin(async {})
    }));
    done_rx.recv().await;
    info! {"[tokio initial] + [WRTC -> UDP SQ] Closing!"};
    RTCDC.close().await.expect("Error closing the connection");

    /* Ok(*/
    (Arc::clone(&RTCDC), OtherSocket) /*)*/
}
async fn handle_offer(
    peer_connection: Arc<RTCPeerConnection>,
    data_channel: Arc<RTCDataChannel>,
    session_description: RTCSessionDescription,
) -> Result<(Arc<RTCPeerConnection>, Arc<RTCDataChannel>), Box<dyn error::Error>> {
    let conn = Arc::clone(&peer_connection);
    data_channel.on_message(Box::new(move |msg: DataChannelMessage| {
        let msg = msg.data.to_vec();
        debug!("Message from DataChannel (before OtherSocket was set up): '{msg:?}'");
        if (OtherSocketSendBuf.lock().len() + msg.len() > MaxOtherSocketSendBufSize) {
            warn! {"Buffer FULL: {} + {} > {}",
            OtherSocketSendBuf.lock().len(),
            msg.len(),
            MaxOtherSocketSendBufSize
            };
        } else {
            OtherSocketSendBuf.lock().extend_from_slice(&msg);
        }
        Box::pin(async {})
    }));
    conn.set_remote_description(session_description).await?;

    Ok((peer_connection, data_channel))
}
fn write_offer_and_read_answer(local: Option<RTCSessionDescription>, config: Config) -> String {
    if let Some(true) = config.Publish {
        if let Some(ref ptype) = config.PublishType {
            match (ptype.as_str()) {
                "ws" => return write_offer_and_read_answer_ws(local, config).unwrap(),
                _ => {
                    log::error!("Unsupported PublishType: {}", ptype);
                    return "".to_string();
                }
            }
        } else {
            return write_offer_and_read_answer_stdio(local, config);
        }
    } else {
        return write_offer_and_read_answer_stdio(local, config);
    }
}
fn write_offer_and_read_answer_ws(local: Option<RTCSessionDescription>, config: Config) -> Option<String> {
    let json_str = serde_json::to_string(&(local.expect("Empty local SD."))).expect("Could not serialize the localDescription to JSON");
    println! {"{}", encode(&json_str)};
    let mut offerBase64Text: String = String::new();
    let mut headers = Headers::new();
    let AuthType: String;
    if let Some(ref _AuthType) = config.PublishAuthType {
        AuthType = _AuthType.to_string();
    } else {
        AuthType = String::from("Basic");
    }
    if (AuthType == "Basic") {
        headers.set(Authorization(Basic {
            username: config.PublishAuthUser.clone().expect("No user specified for WS(S) basic auth."),
            password: config.PublishAuthPass.clone(),
        }));
    } else if (AuthType == "Bearer") {
        headers.set(Authorization(Bearer {
            token: config.PublishAuthUser.clone().expect("No user specified for WS(S) basic auth."),
        }));
    }
    let mut client = ClientBuilder::new(&config.PublishEndpoint.clone().expect("No WS(S) endpoint specified."))
        .unwrap()
        .custom_headers(&headers)
        .connect(None)
        .unwrap();
    if let Some(ref PeerAuthType) = config.PeerAuthType {
        if PeerAuthType == "PSK" {
            let tmessage: TimedMessage = ConstructMessage(encode(&json_str));
            let amessage: HashAuthenticatedMessage = ConstructAuthenticatedMessage(tmessage, config.clone());
            let message = websocket::Message::text(serde_json::to_string(&amessage).expect("Serialization error"));
            client.send_message(&message).expect("WS: Unable to send.");
            let aanswer = client.recv_message().expect("WS: Unable to receive.");
            if let Text(aanswer) = aanswer {
                let AuthenticatedMessage: HashAuthenticatedMessage = serde_json::from_str(&aanswer).expect("Deserialization error.");
                let answer = CheckAndReturn(VerifyAndReturn(AuthenticatedMessage, config).expect("An error occured while deserializing.")).expect("Authentication error.");
                Some(answer)
            } else {
                log::error!("Malformed response received from the WS endpoint");
                None
            }
        } else {
            log::error! {"Unsupported peer authentication type: {}", PeerAuthType};
            None
        }
    } else {
        let tmessage: TimedMessage = ConstructMessage(encode(&json_str));
        let amessage: HashAuthenticatedMessage = ConstructAuthenticatedMessage(tmessage, config.clone());
        let message = websocket::Message::text(serde_json::to_string(&amessage).expect("Serialization error"));
        client.send_message(&message).expect("WS: Unable to send.");
        let aanswer = client.recv_message().expect("WS: Unable to receive.");
        if let Text(aanswer) = aanswer {
            let AuthenticatedMessage: HashAuthenticatedMessage = serde_json::from_str(&aanswer).expect("Deserialization error.");
            let answer = CheckAndReturn(VerifyAndReturn(AuthenticatedMessage, config).expect("An error occured while deserializing.")).expect("Authentication error.");
            Some(answer)
        } else {
            log::error!("Malformed response received from the WS endpoint");
            None
        }
    }
}
fn write_offer_and_read_answer_stdio(local: Option<RTCSessionDescription>, config: Config) -> String {
    let json_str = serde_json::to_string(&(local.expect("Empty local SD."))).expect("Could not serialize the localDescription to JSON");
    println! {"{}", encode(&json_str)};
    let mut offerBase64Text: String = String::new();
    match (config.ConHost) {
        Some(val) => {
            if (val == true) {
                let mut line: String;
                let mut buf: Vec<u8> = vec![];
                BufReader::new(io::stdin()).read_until(b'/', &mut buf);
                let lines: String = String::from(std::str::from_utf8(&buf).expect("Input not UTF-8"));
                line = String::from(lines.replace("\n", "").replace("\r", "").replace(" ", ""));
                let _ = line.pop();
                offerBase64Text = line;
            } else {
                let _ = io::stdin().read_line(&mut offerBase64Text).expect("Cannot read the offer!");
            }
        }
        None => {
            let _ = io::stdin().read_line(&mut offerBase64Text).expect("Cannot read the offer!");
        }
    }
    let offerBase64TextTrimmed = offerBase64Text.trim();
    info! {"Read offer: {}", offerBase64TextTrimmed};
    String::from(offerBase64Text.trim())
}
fn main() {
    env_logger::init();
    let mut arg_counter: usize = 0;
    let mut arg: std::env::Args;
    arg = env::args();
    arg_counter = arg.count();
    info!("Arguments received: {arg_counter}");
    if (arg_counter != 2) {
        println! {"Expecting exactly one argument, the TOML file with connection parameters."}
        #[cfg(unix)]
        {
            println! {"Built on: {}", run_command_str!("uname", "-a")};
        }
        println! {"Version info: {}", run_command_str!("fossil", "timeline", "-n", "+1")};
        println! {"MTU: {}", PKT_SIZE};
        println! {"LICENSE:\n{}", include_str!{"../../LICENSE.txt"}};
        exit(2);
    }
    let TOML_file_name = env::args().nth(1).unwrap();
    info! {"Reading from: {}", TOML_file_name};
    let TOML_file_read = read_to_string(TOML_file_name);
    let TOML_file_contents: String;
    match TOML_file_read {
        Ok(T) => TOML_file_contents = T,
        Err(E) => {
            error! {"{}", E};
            exit(1)
        }
    }
    info! {"{}", TOML_file_contents};
    let config: Config = toml::from_str(&TOML_file_contents).unwrap();
    info!("Configuration: type: {}", config.Type);
    if (config.WebRTCMode == "Offer") {
        //let rt = Runtime::new().unwrap();
        let config1 = config.clone();
        let rt = Builder::new_multi_thread()
            .worker_threads(1)
            .enable_all()
            .thread_stack_size(THREAD_STACK_SIZE)
            .on_thread_start(move || Pinning::Try(config1.PinnedCores, 0))
            .thread_name("TOKIO: main")
            .build()
            .unwrap();
        let (mut data_channel, mut peer_connection, local_description, done_rx, done_tx, cb_done_rx, cb_done_tx) =
            rt.block_on(create_WebRTC_offer(&config)).expect("Failed creating a WebRTC Data Channel.");
        let offerBase64TextTrimmed = write_offer_and_read_answer(local_description, config.clone());
        let offer = decode(&offerBase64TextTrimmed).expect("base64 conversion error");
        let answer = serde_json::from_str::<RTCSessionDescription>(&offer).expect("Error parsing the offer!");
        (peer_connection, data_channel) = rt.block_on(handle_offer(peer_connection, data_channel, answer)).expect("Error acccepting offer!");
        let BindAddress = config.Address.clone().expect("Binding address not specified");
        let Watchdog = thread::Builder::new().stack_size(THREAD_STACK_SIZE).name("Watchdog".to_string()).spawn(move || {
            debug! {"Inactivity monitoring watchdog has started"}
            loop {
                let five_seconds = time::Duration::from_secs(600);
                debug! {"Watchdog will sleep for five seconds."};
                let current_time: u64 = chrono::Utc::now().timestamp().try_into().expect("This software is not supposed to be used before UNIX was invented.");
                debug! {
                    "Stream was last active {} seconds ago. The current time is: {}. Last active time: {}.",
                    current_time - STREAM_LAST_ACTIVE_TIME.load(Ordering::Relaxed),
                    current_time,
                    STREAM_LAST_ACTIVE_TIME.load(Ordering::Relaxed)
                };
                thread::sleep(five_seconds);
                debug! {"Watchdog: Resuming..."};
            }
        });
        let mut buf = [0; PKT_SIZE];
        if (config.Type == "UDP") {
            info! {"UDP socket requested"};
            let BindPort = config.Port.clone().expect("Binding port not specified");
            info! {"Binding UDP on address {} port {}", BindAddress, BindPort};
            let mut OtherSocket = UdpSocket::bind(format! {"{}:{}", BindAddress, BindPort}).expect(&format! {"Could not bind to UDP port: {}:{}", &BindAddress, &BindPort});
            info! {"Bound UDP on address {} port {}", BindAddress, BindPort};
            OtherSocket.set_read_timeout(Some(Duration::new(1 * config.TimeoutCountMax.unwrap_or(3), 0)));
            let (amt, src) = OtherSocket.peek_from(&mut buf).expect("Error saving to buffer");
            info!("UDP connecting to: {}", src);
            OtherSocket.connect(src).expect(&format! {"UDP connect error: connect() to {}", src});
            OtherSocket.set_read_timeout(Some(Duration::new(1, 0)));
            STREAM_LAST_ACTIVE_TIME.store(
                chrono::Utc::now().timestamp().try_into().expect("This software is not supposed to be used before UNIX was invented."),
                Ordering::Relaxed,
            );
            (data_channel, OtherSocket) = rt.block_on(configure_send_receive_udp(data_channel, OtherSocket, done_rx, done_tx, cb_done_rx, cb_done_tx, config.clone()));
        } else if (config.Type == "UDD") {
            #[cfg(feature = "udd")]
            {
                info! {"Unix Domain Socket requested."};
                let Listener = UnixListener::bind(BindAddress);
                let mut OtherSocket = Listener.expect("UDS listen error").incoming().next().expect("Error getting the UDS stream").expect("UDS stream error");
                (data_channel, OtherSocket) = rt.block_on(configure_send_receive_uds(data_channel, OtherSocket));
            }
            #[cfg(not(feature = "udd"))]
            {
                println! {"Feature available but nor enabled: UDD."};
            }
        } else {
            println! {"Unsupported type: {}", config.Type};
        }
    } else {
        println! {"Unsupported WebRTC Mode: {}. Probably the WRONG TOOL.", config.WebRTCMode};
    }
}
