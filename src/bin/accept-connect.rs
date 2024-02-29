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
use crossbeam_channel::RecvTimeoutError;
use futures::executor::block_on;
use lazy_static::lazy_static;
use log::{debug, error, info, trace, warn};
use parking_lot::Mutex;
use serde::Deserialize;
use std::env;
use std::error;
use std::error::Error;

use crossbeam_channel::{bounded, Receiver, Sender};
use std::fs::read_to_string;
use std::io;
use std::io::Read;
use std::io::Result as IOResult;
use std::io::Write;
use std::io::{BufRead, BufReader};
use std::io::{Error as ioError, ErrorKind};
use std::time::Duration;

use std::net::TcpStream;
use std::net::UdpSocket;
#[cfg(unix)]
use std::os::unix::net::UnixStream;
use std::process::exit;
use std::sync::atomic::Ordering;
use std::sync::atomic::{AtomicBool, AtomicU64};
use std::sync::Arc;
use std::thread;
use std::thread::JoinHandle;
use std::time;
use tokio::runtime::Builder;
use tokio::runtime::Runtime;
//TOKIO UNSTABLE use tokio::runtime::UnhandledPanic;

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

use std::future::Future;
use std::path::Path;
use std::pin::Pin;
use std::task::{Context, Poll};

use mimalloc::MiMalloc;

#[global_allocator]
static GLOBAL: MiMalloc = MiMalloc;

static STREAM_LAST_ACTIVE_TIME: AtomicU64 = AtomicU64::new(0);
static OtherSocketReady: AtomicBool = AtomicBool::new(false);
static DataChannelReady: AtomicBool = AtomicBool::new(false);
static CAN_RECV: AtomicBool = AtomicBool::new(true);
static MaxOtherSocketSendBufSize: usize = 2048;
static THREAD_STACK_SIZE: usize = 204800;
const PKT_SIZE: usize = 2040;

lazy_static! {
    static ref OtherSocketSendBuf: Mutex<Vec<u8>> = Mutex::new(Vec::new());
    static ref Threads: Mutex<Vec<JoinHandle<()>>> = Mutex::new(Vec::new());
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
async fn accept_WebRTC_offer(
    offer: RTCSessionDescription,
    config: &Config,
) -> Result<
    (
        Arc<RTCDataChannel>,
        Arc<RTCPeerConnection>,
        Arc<RTCSessionDescription>,
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
    let fwconfig = config;
    let config = RTCConfiguration {
        ice_servers: ice_servers,
        ..Default::default()
    };

    // Create a new RTCPeerConnection
    let peer_connection = Arc::new(api.new_peer_connection(config).await?);
    let (cb_done_tx, cb_done_rx): (Sender<bool>, Receiver<bool>) = bounded::<bool>(4);

    // Create a datachannel with label 'data'
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
    let cb_done_tx_2 = cb_done_tx.clone();
    let (done_tx_2) = (done_tx.clone());

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

    // Sets the RemoteDescription, and starts our UDP listeners
    peer_connection.set_remote_description(offer).await?;
    // Create an offer to send to the browser
    let answer = peer_connection.create_answer(None).await?;

    // Create channel that is blocked until ICE Gathering is complete
    let mut gather_complete = peer_connection.gathering_complete_promise().await;

    peer_connection.set_local_description(answer.clone()).await?;

    // Block until ICE Gathering is complete, disabling trickle ICE
    // we do this because we only can exchange one signaling message
    // in a production application you should exchange ICE Candidates via OnICECandidate
    let _ = gather_complete.recv().await;
    //RTCPC.on_data_channel(Box::new(move |d: Arc<RTCDataChannel>| {}))

    let local_description: Option<RTCSessionDescription>;

    // Output the answer in base64 so we can paste it in browser
    if let Some(local_desc) = peer_connection.local_description().await {
        local_description = Some(local_desc.clone());
        write_answer(Some(local_desc.clone()), fwconfig.clone());
        /*let json_str = serde_json::to_string(&local_desc)?;
        //let b64 = encode(&json_str);
        info!("{json_str}");
        let b64 = encode(&json_str);
        info!("{b64}");*/

        //println!("{b64}");
    } else {
        local_description = None;
        info!("generate local_description failed!");
    }
    Ok((
        Arc::clone(&data_channel),
        peer_connection,
        Arc::new(answer),
        local_description,
        done_rx,
        done_tx_2,
        cb_done_rx,
        cb_done_tx_2,
    ))
}
pub trait Socket: Send + Sync + Unpin + Read + Write {
    fn read<B: Read>(&mut self, buf: &mut [u8]) -> IOResult<usize>
    where
        Self: Sized,
    {
        std::io::Read::read(self, buf)
        //Read { socket: self, buf }
    }

    fn write<B: Write>(&mut self, buf: &[u8]) -> IOResult<usize>
    where
        Self: Sized,
    {
        std::io::Write::write(self, buf)
        //Write { socket: self, buf }
    }

    fn flush(&mut self) -> IOResult<()>
    where
        Self: Sized,
    {
        std::io::Write::flush(self)
        //Flush { socket: self }
    }
}
async fn configure_send_receive_udp(
    RTCDC: Arc<RTCDataChannel>,
    RTCPC: Arc<RTCPeerConnection>,
    OtherSocket: UdpSocket,
    mut done_rx: tokio::sync::mpsc::Receiver<()>,
    mut done_tx: tokio::sync::mpsc::Sender<()>,
    Done_rx: crossbeam_channel::Receiver<bool>,
    cb_done_tx: crossbeam_channel::Sender<bool>,
    config: Config,
) -> (Arc<RTCDataChannel>, UdpSocket) /*, Box<dyn error::Error>>*/ {
    // Register channel opening handling
    let d1 = Arc::clone(&RTCDC);
    let mut ClonedSocketRecv = OtherSocket.try_clone().expect("Unable to clone the TCP socket. :(");
    let mut ClonedSocketSend = OtherSocket.try_clone().expect("Unable to clone the TCP socket. :(");
    Pinning::Try(config.PinnedCores, 0);
    let config2 = config.clone();
    let config3 = config.clone();
    let config4 = config.clone();

    RTCPC.on_data_channel(Box::new(move |d: Arc<RTCDataChannel>| {
        let (OtherSocketSendQueue_tx, OtherSocketSendQueue_rx): (Sender<AlignedMessage>, Receiver<AlignedMessage>) = bounded::<AlignedMessage>(128);
        let d_label = d.label().to_owned();
        let d_id = d.id();
        info!("New DataChannel {d_label} {d_id}");
        #[cfg(feature = "queuemon")]
        let OtherSocketSendQueue_tx_c = OtherSocketSendQueue_tx.clone();

        // Register channel opening handling
        Box::pin({
            let d1 = d1.clone();
            let d2 = d1.clone();
            let cb_done_tx = cb_done_tx.clone();
            let done_tx = done_tx.clone();
            let (mut ClonedSocketRecv, mut ClonedSocketSend) = (ClonedSocketRecv.try_clone().expect(""), ClonedSocketSend.try_clone().expect(""));
            let Done_rx = Done_rx.clone();
            async move {
                let d1 = Arc::clone(&d1);
                let d2 = Arc::clone(&d);
                let d3 = Arc::clone(&d);

                let d_label2 = d_label.clone();
                let d_id2 = d_id;
                let (mut ClonedSocketRecv, mut ClonedSocketSend) = (ClonedSocketRecv.try_clone().expect(""), ClonedSocketSend.try_clone().expect(""));
                d.on_close(Box::new(move || {
                    info! {"[tokio initial] + [WRTC -> UDP SQ] DC closed."};
                    Box::pin(async {})
                }));
                d.on_open(Box::new({
                    let d1 = d1.clone();
                    let Done_rx_2 = Done_rx.clone();
                    let Done_rx_3 = Done_rx.clone();
                    move || {
                        let (WebRTCSendQueue_tx, WebRTCSendQueue_rx): (Sender<AlignedMessage>, Receiver<AlignedMessage>) = bounded::<AlignedMessage>(128);
                        #[cfg(feature = "queuemon")]
                        {
                            let WebRTCSendQueue_tx = WebRTCSendQueue_tx.clone();
                            let OtherSocketSendQueue_tx = OtherSocketSendQueue_tx_c.clone();
                            let QueueMon = thread::Builder::new().name("QueueMon".to_string()).spawn(move || {
                                debug! {"Inactivity monitoring watchdog has started"}
                                loop {
                                    let five_seconds = time::Duration::from_secs(10);
                                    info! {"WebRTC SendQueue: {}, OtherSocketSendQueue: {}.", WebRTCSendQueue_tx.len(), OtherSocketSendQueue_tx.len()};
                                    debug! {"QueueMon will sleep for five seconds."};
                                    thread::sleep(five_seconds);
                                    debug! {"Watchdog: Resuming..."};
                                }
                            });
                        }
                        info!("Data channel '{d_label2}'-'{d_id2}' open.");
                        let d1 = d1.clone();
                        let d = d1.clone();
                        let done_tx2 = done_tx.clone();
                        let cb_done_tx2 = cb_done_tx.clone();
                        let cu_udp_sq_to_udp = move || {
                            info! {"UDP SQ -> UDP started."};
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
                                                block_on(d.close());
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

                                //if (CAN_RECV.load(Ordering::Relaxed)) {

                                /*}
                                //#[cold]
                                else {
                                    if (OtherSocketSendBuf.lock().len() + msg.len() > MaxOtherSocketSendBufSize) {
                                        warn! {"Buffer FULL: {} + {} > {}",
                                        OtherSocketSendBuf.lock().len(),
                                        msg.len(),
                                        MaxOtherSocketSendBufSize
                                        };
                                    } else {
                                        debug! {"OtherSocket not ready yet!"};
                                        OtherSocketSendBuf.lock().extend_from_slice(&msg);
                                    }
                                }*/
                            }
                            info! {"Exiting UDP SQ -> UDP concurrency unit (gracefully)"};
                        };
                        info! {"Spawning: UDP SQ -> UDP"};
                        let udp_sq_to_udp = thread::Builder::new()
                            .name("UDP SQ -> UDP".to_string())
                            .stack_size(THREAD_STACK_SIZE)
                            .spawn(cu_udp_sq_to_udp)
                            .expect("Unable to spawn: UDP SQ -> UDP");
                        Threads.lock().push(udp_sq_to_udp);
                        let cu_udp_to_wrtc_sq = move || {
                            Pinning::Try(config3.PinnedCores, 2);
                            log::info! {"Spawned thread: UDP -> WRTC SQ."};
                            let (mut ClonedSocketRecv) = (ClonedSocketRecv.try_clone().expect(""));
                            let no_data_count_max: u64 = config.TimeoutCountMax.unwrap_or(3 as u64);
                            let mut no_data_counter: u64 = 0;
                            loop {
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
                                        trace! {"{:?}", &buf[0..amt]};
                                        debug! {"Enqueued..."};
                                        no_data_counter = 0;
                                        let _ = WebRTCSendQueue_tx.try_send(AlignedMessage { size: amt, data: buf.into() });
                                    }
                                    Err(E) => match (E.kind()) {
                                        std::io::ErrorKind::WouldBlock => {
                                            no_data_counter += 1;
                                            trace!("Unable to read or save to the buffer: {:?}", E);
                                            trace! {"Restarting the loop due to previous error: OtherSocket (read) => DataChannel (write)"};
                                        }
                                        std::io::ErrorKind::TimedOut => {
                                            no_data_counter += 1;
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
                            }
                            info! {"[UDP -> WRTC SQ] Exiting thread (gracefully)..."};
                        };
                        let udp_to_wrtc_sq = thread::Builder::new()
                            .name("OS->DC".to_string())
                            .stack_size(THREAD_STACK_SIZE)
                            .spawn(cu_udp_to_wrtc_sq)
                            .expect("Unable to spawn thread: UDP recv => WRTC SQ.");
                        Threads.lock().push(udp_to_wrtc_sq);
                        let cu_wrtc_sq_to_wrtc = move || {
                            info! {"Spawned the thread: WRTC SQ (read) => DataChannel (write)"};
                            let rt = Builder::new_multi_thread()
                                .worker_threads(1)
                                // TOKIO UNSTABLE .unhandled_panic(UnhandledPanic::ShutdownRuntime)
                                .thread_stack_size(THREAD_STACK_SIZE)
                                .thread_name("TOKIO: OS->DC")
                                .on_thread_start(move || Pinning::Try(config2.PinnedCores, 1))
                                .build()
                                .unwrap();
                            let d1 = d1.clone();
                            info! {"WRTC SQ -> WRTC"};

                            loop {
                                if let Ok(MessageWithSize) = WebRTCSendQueue_rx.recv_timeout(Duration::from_secs(1)) {
                                    debug! {"Enqueued to WebRTC send queue: {MessageWithSize:?}, Queue size: {}.", WebRTCSendQueue_rx.len()};
                                    let buf = MessageWithSize.data;
                                    let amt = MessageWithSize.size;
                                    let d1 = d1.clone();
                                    debug! {"Blocking on DC send... Len: {}", amt};
                                    let written_bytes = rt.block_on(d3.send(&Bytes::copy_from_slice(&buf[0..amt])));
                                    match (written_bytes) {
                                        Ok(Bytes) => {
                                            debug! {"OS->DC: Written {Bytes} bytes!"};
                                        }
                                        #[cold]
                                        Err(E) => {
                                            info! {"DataConnection {}: unable to send: {:?}.",
                                            d1.label(),
                                            E};
                                            info! {"Breaking the loop due to previous error: OtherSocket (read) => DataChannel (write)"};
                                            break;
                                        }
                                    }
                                }
                                if Done_rx_2.try_recv() == Ok(true) {
                                    break;
                                }
                            }
                            info! {"[WRTC SQ -> WRTC] Exiting thread (gracefully)..."};
                        };
                        let wrtc_sq_to_wrtc = thread::Builder::new().name("OS->DC".to_string()).stack_size(THREAD_STACK_SIZE).spawn(cu_wrtc_sq_to_wrtc);
                        match (wrtc_sq_to_wrtc) {
                            Ok(JH) => Threads.lock().push(JH),
                            Err(E) => {
                                error! {"Unable to spawn: {:?}", E}
                            }
                        }
                        Box::pin(async move {})
                    }
                }));
                // Register text message handling
                d.on_message(Box::new({
                    //let d = d2.clone();
                    move |msg: DataChannelMessage| {
                        let msg = msg.data.to_vec();
                        trace!("Message from DataChannel '{d_label}': '{msg:?}'");
                        debug!("Message from DataChannel '{d_label}'.: '{msg:?}', size: {}'", msg.len());
                        debug! {"Blocking on UDP send queue - Enque"};
                        OtherSocketSendQueue_tx.try_send(AlignedMessage { size: 0, data: msg });
                        debug! {"UDP send queue enqueue block is over."};
                        Box::pin(async {})
                    }
                }));
            }
        })
    }));

    debug! {"Successfully registered the on_message handle"};
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
    Ok((peer_connection, data_channel))
}
fn read_offer(config: Config) -> String {
    if let Some(true) = config.Publish {
        if let Some(ref ptype) = config.PublishType {
            match (ptype.as_str()) {
                "ws" => return read_offer_ws(config).unwrap(),
                _ => {
                    log::error!("Unsupported PublishType: {}", ptype);
                    return "".to_string();
                }
            }
        } else {
            return read_offer_stdio(config);
        }
    } else {
        return read_offer_stdio(config);
    }
}
fn read_offer_stdio(config: Config) -> String {
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
    offerBase64Text
}
fn read_offer_ws(config: Config) -> Option<String> {
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
        None
    }
}
fn write_answer(local: Option<RTCSessionDescription>, config: Config) -> Result<(), Box<dyn error::Error>> {
    if let Some(true) = config.Publish {
        if let Some(ref ptype) = config.PublishType {
            match (ptype.as_str()) {
                "ws" => return write_answer_ws(local, config),
                _ => {
                    log::error!("Unsupported PublishType: {}", ptype);
                    return Ok(());
                }
            }
        } else {
            return Ok(write_answer_stdio(local, config));
        }
    } else {
        return Ok(write_answer_stdio(local, config));
        /* return Err(Box::new(ioError::new(
            ErrorKind::Other,
            "Websocket write failed.",
        ))); */
    }
}
fn write_answer_stdio(local: Option<RTCSessionDescription>, config: Config) -> () {
    let json_str = serde_json::to_string(&(local.expect("Empty local SD."))).expect("Could not serialize the localDescription to JSON");
    println! {"{}", encode(&json_str)};
}
fn write_answer_ws(local: Option<RTCSessionDescription>, config: Config) -> Result<(), Box<dyn Error>> {
    let json_str = serde_json::to_string(&(local.expect("Empty local SD."))).expect("Could not serialize the localDescription to JSON");
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
            Ok(())
        } else {
            log::error! {"Unsupported peer authentication type: {}", PeerAuthType};
            Err(Box::new(ioError::new(ErrorKind::InvalidInput, "Invalid PeerAuthType.")))
        }
    } else {
        let tmessage: TimedMessage = ConstructMessage(encode(&json_str));
        let amessage: HashAuthenticatedMessage = ConstructAuthenticatedMessage(tmessage, config.clone());
        let message = websocket::Message::text(serde_json::to_string(&amessage).expect("Serialization error"));
        client.send_message(&message).expect("WS: Unable to send.");
        Ok(())
    }
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
    let config1 = config.clone();
    if (config.WebRTCMode == "Accept") {
        //let rt = Runtime::new().unwrap();
        let rt = Builder::new_multi_thread()
            .worker_threads(1)
            .thread_stack_size(THREAD_STACK_SIZE)
            .enable_all()
            //TOKIO UNSTABLE .unhandled_panic(UnhandledPanic::ShutdownRuntime)
            .on_thread_start(move || Pinning::Try(config1.PinnedCores, 0))
            .thread_name("TOKIO: main")
            .build()
            .unwrap();

        let offerBase64TextTrimmed = read_offer(config.clone());
        info! {"Read offer: {}", offerBase64TextTrimmed};
        let offer = decode(&offerBase64TextTrimmed).expect("base64 conversion error");
        let offerRTCSD = serde_json::from_str::<RTCSessionDescription>(&offer).expect("Error parsing the offer!");
        let (mut data_channel, mut peer_connection, mut answer, local_description, done_rx, done_tx, cb_done_rx, cb_done_tx) =
            rt.block_on(accept_WebRTC_offer(offerRTCSD, &config)).expect("Failed creating a WebRTC Data Channel.");
        (peer_connection, data_channel) = rt.block_on(handle_offer(peer_connection, data_channel, (*answer).clone())).expect("Error acccepting offer!");
        let ConnectAddress = config.Address.clone().expect("Binding address not specified");
        let Watchdog = thread::Builder::new().name("Watchdog".to_string()).spawn(move || {
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
                for thread in Threads.lock().iter() {
                    if (thread.is_finished()) {
                        info! {"Done, dead or killed: {:?}", thread};
                    } else {
                        info! {"Alive: {:?}", thread};
                    }
                }
                thread::sleep(five_seconds);
                debug! {"Watchdog: Resuming..."};
            }
        });
        let mut buf = [0; PKT_SIZE];
        if (config.Type == "UDP") {
            info! {"UDP socket requested"};
            let ConnectPort = config.Port.clone().expect("Connecting port not specified");
            info! {"Connecting to UDP to address {} port {}", ConnectAddress, ConnectPort};
            let mut OtherSocket = UdpSocket::bind("0.0.0.0:0").expect("UDP Socket: unable to bind: 0.0.0.0:0.");
            /*.connect(format! {"{}:{}", ConnectAddress, ConnectPort})
            .expect(&format! {
                "Could not connect to UDP port: {}:{}", &ConnectAddress, &ConnectPort
            });*/
            info! {"Connected UDP on address {} port {}", ConnectAddress, ConnectPort};
            OtherSocket
                .connect(format!("{}:{}", ConnectAddress, ConnectPort))
                .expect(&format! {"UDP connect error: connect() to {}", format!{
                    "{}:{}", ConnectAddress, ConnectPort
                }});
            OtherSocket.set_read_timeout(Some(Duration::from_secs(1)));
            STREAM_LAST_ACTIVE_TIME.store(
                chrono::Utc::now().timestamp().try_into().expect("This software is not supposed to be used before UNIX was invented."),
                Ordering::Relaxed,
            );
            (data_channel, OtherSocket) = rt.block_on(configure_send_receive_udp(
                data_channel,
                peer_connection,
                OtherSocket,
                done_rx,
                done_tx,
                cb_done_rx,
                cb_done_tx,
                config.clone(),
            ));
        } else if (config.Type == "TCP") {
            #[cfg(feature = "tcp")]
            {
                info! {"TCP socket requested"};
                let ConnectPort = config.Port.clone().expect("Connecting port not specified");
                info! {"Connecting TCP on address {} port {}", ConnectAddress, ConnectPort};
                let mut OtherSocket = TcpStream::connect(format!("{}:{}", ConnectAddress, ConnectPort)).expect("Error getting the TCP stream");
                debug! {"Attempting to write the send buffer: {:?}", &OtherSocketSendBuf.lock()};
                OtherSocket.write(&OtherSocketSendBuf.lock());
                info! {"Connected to TCP: address {} port {}", ConnectAddress, ConnectPort};
                match (OtherSocket.set_nodelay(true)) {
                    Ok(_) => debug! {"NODELAY set"},
                    Err(_) => warn!("SO_NODELAY failed."),
                }
                STREAM_LAST_ACTIVE_TIME.store(
                    chrono::Utc::now().timestamp().try_into().expect("This software is not supposed to be used before UNIX was invented."),
                    Ordering::Relaxed,
                );
                (data_channel, OtherSocket) = rt.block_on(configure_send_receive_tcp(data_channel, peer_connection, OtherSocket));
            }
            #[cfg(not(feature = "tcp"))]
            {
                println! {"Feature available but not enabled: TCP."};
            }
        } else if (config.Type == "UDS") {
            #[cfg(feature = "uds")]
            {
                info! {"Unix Domain Socket requested."};
                let mut OtherSocket = UnixStream::connect(ConnectAddress).expect("UDS connect error");
                (data_channel, OtherSocket) = rt.block_on(configure_send_receive_uds(data_channel, peer_connection, OtherSocket));
            }
            #[cfg(not(feature = "uds"))]
            {
                println! {"Feature available but not enabled: UDS."};
            }
        } else {
            println! {"Unsupported type: {}", config.Type};
        }
    } else {
        println! {"Unsupported WebRTC Mode: {}. Probably the WRONG TOOL.", config.WebRTCMode};
    }
}
