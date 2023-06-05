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
use futures::executor::block_on;
use lazy_static::lazy_static;
use log::{debug, error, info, trace, warn};
use parking_lot::Mutex;
use serde::Deserialize;
use std::env;
use std::error;

use std::fs::read_to_string;
use std::io;
use std::io::Read;
use std::io::Write;

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
use tokio::runtime::Runtime;

#[cfg(windows)]
use uds_windows::{UnixListener, UnixStream};
use webrtc::api::interceptor_registry::register_default_interceptors;
use webrtc::api::media_engine::MediaEngine;
use webrtc::api::APIBuilder;
use webrtc::data_channel::data_channel_message::DataChannelMessage;
use webrtc::data_channel::RTCDataChannel;
use webrtc::ice_transport::ice_server::RTCIceServer;
use webrtc::interceptor::registry::Registry;
use webrtc::peer_connection::configuration::RTCConfiguration;

use webrtc::peer_connection::peer_connection_state::RTCPeerConnectionState;
use webrtc::peer_connection::sdp::session_description::RTCSessionDescription;
use webrtc::peer_connection::RTCPeerConnection;

static STREAM_LAST_ACTIVE_TIME: AtomicU64 = AtomicU64::new(0);
static OtherSocketReady: AtomicBool = AtomicBool::new(false);
static DataChannelReady: AtomicBool = AtomicBool::new(false);
static CAN_RECV: AtomicBool = AtomicBool::new(true);
static MaxOtherSocketSendBufSize: usize = 2048;
static THREAD_STACK_SIZE: usize = 204800;

lazy_static! {
    static ref OtherSocketSendBuf: Mutex<Vec<u8>> = Mutex::new(Vec::new());
    static ref Threads: Mutex<Vec<JoinHandle<()>>> = Mutex::new(Vec::new());
}

#[derive(Deserialize)]
struct Config {
    Type: String,
    WebRTCMode: String,
    Address: Option<String>,
    Port: Option<String>,
    ICEServers: Vec<String>,
    Ordered: Option<bool>,
}
#[derive(Deserialize)]
struct ICEServer {
    URL: String,
    Username: Option<String>,
    Credential: Option<String>,
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
    ),
    Box<dyn error::Error>,
> {
    // Create a MediaEngine object to configure the supported codec
    let mut m = MediaEngine::default();

    // Register default codecs
    m.register_default_codecs()
        .expect("Could not register the default codecs.");

    // Create a InterceptorRegistry. This is the user configurable RTP/RTCP Pipeline.
    // This provides NACKs, RTCP Reports and other features. If you use `webrtc.NewPeerConnection`
    // this is enabled by default. If you are manually managing You MUST create a InterceptorRegistry
    // for each PeerConnection.
    let mut registry = Registry::new();

    // Use the default set of Interceptors
    registry = register_default_interceptors(registry, &mut m)
        .expect("Could not register the interceptor!");

    // Create the API object with the MediaEngine
    let api = APIBuilder::new()
        .with_media_engine(m)
        .with_interceptor_registry(registry)
        .build();

    // Prepare the configuration
    let config = RTCConfiguration {
        ice_servers: vec![RTCIceServer {
            urls: config.ICEServers.clone(),
            ..Default::default()
        }],
        ..Default::default()
    };

    // Create a new RTCPeerConnection
    let peer_connection = Arc::new(api.new_peer_connection(config).await?);

    // Create a datachannel with label 'data'
    let data_channel = peer_connection.create_data_channel("data", None).await?;

    let (done_tx, mut done_rx) = tokio::sync::mpsc::channel::<()>(1);

    // Set the handler for Peer connection state
    // This will notify you when the peer has connected/disconnected
    peer_connection.on_peer_connection_state_change(Box::new(move |s: RTCPeerConnectionState| {
        info!("Peer Connection State has changed: {s}");

        if s == RTCPeerConnectionState::Failed {
            // Wait until PeerConnection has had no network activity for 30 seconds or another failure. It may be reconnected using an ICE Restart.
            // Use webrtc.PeerConnectionStateDisconnected if you are interested in detecting faster timeout.
            // Note that the PeerConnection may come back from PeerConnectionStateDisconnected.
            info!("Peer Connection has gone to failed exiting");
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

    peer_connection
        .set_local_description(answer.clone())
        .await?;

    // Block until ICE Gathering is complete, disabling trickle ICE
    // we do this because we only can exchange one signaling message
    // in a production application you should exchange ICE Candidates via OnICECandidate
    let _ = gather_complete.recv().await;
    //RTCPC.on_data_channel(Box::new(move |d: Arc<RTCDataChannel>| {}))

    // Output the answer in base64 so we can paste it in browser
    if let Some(local_desc) = peer_connection.local_description().await {
        let json_str = serde_json::to_string(&local_desc)?;
        //let b64 = encode(&json_str);
        info!("{json_str}");
        let b64 = encode(&json_str);
        info!("{b64}");
        println!("{b64}");
    } else {
        info!("generate local_description failed!");
    }
    Ok((Arc::clone(&data_channel), peer_connection, Arc::new(answer)))
}
async fn configure_send_receive_udp(
    RTCDC: Arc<RTCDataChannel>,
    RTCPC: Arc<RTCPeerConnection>,
    OtherSocket: UdpSocket,
) -> (Arc<RTCDataChannel>, UdpSocket) /*, Box<dyn error::Error>>*/ {
    // Register channel opening handling
    let d1 = Arc::clone(&RTCDC);
    let mut ClonedSocketRecv = OtherSocket
        .try_clone()
        .expect("Unable to clone the TCP socket. :(");
    let mut ClonedSocketSend = OtherSocket
        .try_clone()
        .expect("Unable to clone the TCP socket. :(");
    RTCPC.on_data_channel(Box::new(move |d: Arc<RTCDataChannel>| {
        let d_label = d.label().to_owned();
        let d_id = d.id();
        info!("New DataChannel {d_label} {d_id}");

        // Register channel opening handling
        Box::pin({
            let d1 = d1.clone();

            let (mut ClonedSocketRecv, mut ClonedSocketSend) = (
                ClonedSocketRecv.try_clone().expect(""),
                ClonedSocketSend.try_clone().expect(""),
                );
            async move {
                let d1 = Arc::clone(&d1);
                let d2 = Arc::clone(&d);
                let d_label2 = d_label.clone();
                let d_id2 = d_id;
                let (mut ClonedSocketRecv, mut ClonedSocketSend) = (
                    ClonedSocketRecv.try_clone().expect(""),
                    ClonedSocketSend.try_clone().expect(""),
                    );
                d.on_open(Box::new({let d1 = d1.clone(); move || {
                    info!("Data channel '{d_label2}'-'{d_id2}' open.");
                    let d1=d1.clone();

                    Box::pin(async move {
                        thread::spawn(move || {
                            let d1 = d1.clone();
                            let (mut ClonedSocketRecv) = (ClonedSocketRecv.try_clone().expect(""));
                            let mut result = Result::<usize>::Ok(0);
                            while result.is_ok() {
                                {
                                    //let mut ready = CAN_RECV.lock(); //.unwrap();
                                    if (CAN_RECV.load(Ordering::Relaxed) == false) {
                                        let mut temp: String = String::new();
                                        println! {"Please press RETURN when you are ready to connect."};
                                        let _ = io::stdin().read_line(&mut temp);
                                        CAN_RECV.store(true, Ordering::Relaxed);
                                    }
                                    //drop(ready);
                                };
                                let d1=d1.clone();
                                let mut buf = [0; 65507];
                                let amt = ClonedSocketRecv
                                    .recv(&mut buf)
                                    .expect("Unable to read or save to the buffer");
                                debug! {"{:?}", &buf[0..amt]};
                                let written_bytes = block_on(d2.send(&Bytes::copy_from_slice(&buf[0..amt])))
                                    .expect(&format! {"DataConnection {}: unable to send.", d1.label()});
                                debug!{"Written!"};
                            }});
                    })
                }}));

                // Register text message handling
                d.on_message(Box::new(move |msg: DataChannelMessage| {
                    let (mut ClonedSocketSend) = (ClonedSocketSend.try_clone().expect(""));
                    let msg = msg.data.to_vec();
                    debug!("Message from DataChannel '{d_label}': '{msg:?}'");
                    ClonedSocketSend.send(&msg).expect("Unable to write data.");
                    //ClonedSocketSend.flush();
                    Box::pin(async {})
                }));
            }
        })
    }));

    debug! {"Successfully registered the on_message handle"};
    let (done_tx, mut done_rx) = tokio::sync::mpsc::channel::<()>(1);
    done_rx.recv().await;
    debug! {"Closing!"};
    RTCDC.close().await.expect("Error closing the connection");

    /* Ok(*/
    (Arc::clone(&RTCDC), OtherSocket) /*)*/
}
async fn configure_send_receive_tcp(
    RTCDC: Arc<RTCDataChannel>,
    RTCPC: Arc<RTCPeerConnection>,
    OtherSocket: TcpStream,
) -> (Arc<RTCDataChannel>, TcpStream) /*, Box<dyn error::Error>>*/ {
    // Register channel opening handling
    let d1 = Arc::clone(&RTCDC);
    let mut ClonedSocketRecv = OtherSocket
        .try_clone()
        .expect("Unable to clone the TCP socket. :(");
    let mut ClonedSocketSend = OtherSocket
        .try_clone()
        .expect("Unable to clone the TCP socket. :(");
    RTCPC.on_data_channel(Box::new(move |d: Arc<RTCDataChannel>| {
        let d_label = d.label().to_owned();
        let d_id = d.id();
        info!("New DataChannel {d_label} {d_id}");

        // Register channel opening handling
        Box::pin({
            let d1 = d1.clone();

            let (mut ClonedSocketRecv, mut ClonedSocketSend) = (
                ClonedSocketRecv.try_clone().expect(""),
                ClonedSocketSend.try_clone().expect(""),
                );
            async move {
                let d1 = Arc::clone(&d1);
                let d2 = Arc::clone(&d);
                let d_label2 = d_label.clone();
                let d_id2 = d_id;
                let (mut ClonedSocketRecv, mut ClonedSocketSend) = (
                    ClonedSocketRecv.try_clone().expect(""),
                    ClonedSocketSend.try_clone().expect(""),
                    );
                d.on_open(Box::new({let d1 = d1.clone(); move || {

                    info!("Data channel '{d_label2}'-'{d_id2}' open.");
                    let d1=d1.clone();
                    let spawned = thread::Builder::new().stack_size(THREAD_STACK_SIZE).spawn(move || {
                        info!{"Spawned the thread: OtherSocket (read) => DataChannel (write)"};
                        let d1 = d1.clone();
                        let (mut ClonedSocketRecv) = (ClonedSocketRecv.try_clone().expect(""));
                        loop {
                            /*{
                                //let mut ready = CAN_RECV.lock(); //.unwrap();
                                if (CAN_RECV.load(Ordering::Relaxed) == false) {
                                    let mut temp: String = String::new();
                                    println! {"Please press RETURN when you are ready to connect."};
                                    let _ = io::stdin().read_line(&mut temp);
                                    CAN_RECV.store(true, Ordering::Relaxed);
                                }
                                //drop(ready);
                            };*/
                            let d1=d1.clone();
                            let mut buf = [0; 65507];
                            let amt = ClonedSocketRecv
                                .read(&mut buf);
                            match (amt){

                                Ok(amt) => {
                                    trace! {"{:?}", &buf[0..amt]};
                                    let written_bytes = block_on(d2.send(&Bytes::copy_from_slice(&buf[0..amt])));
                                    match(written_bytes) {
                                        Ok(Bytes) => {debug!{"Written!"};},
                                        Err(E) => {
                                            info!{"DataConnection {}: unable to send: {:?}.",
                                            d1.label(),
                                            E};
                                            info!{"Breaking the loop due to previous error: OtherSocket (read) => DataChannel (write)"};
                                            break;
                                        }
                                    }
                                },
                                Err(E) => {
                                    info!{"OtherSocket: Connection closed."};
                                    info!{"{:?}", E};
                                    info!{"Breaking the loop due to previous error: OtherSocket (read) => DataChannel (write)"};
                                    break;
                                }
                            }
                        }});
                    match(spawned){
                        Ok(JH)=>{Threads.lock().push(JH)},
                        Err(E) =>{error!{"Unable to spawn: {:?}", E}} 
                    }

                    Box::pin(async move {
                    })
                }}));


                // Register text message handling
                d.on_message(Box::new({let d=d.clone();
                    move |msg: DataChannelMessage| {
                        let msg = msg.data.to_vec();
                        trace!("Message from DataChannel '{d_label}': '{msg:?}'");
                        if (CAN_RECV.load(Ordering::Relaxed)){
                            let (mut ClonedSocketSend) = (ClonedSocketSend.try_clone().expect(""));
                            match(
                                ClonedSocketSend.write(&msg)
                                )
                            {
                                Ok(amt) => {
                                    trace!{"Written {} bytes.", amt};
                                    ClonedSocketSend.flush().expect("Unable to flush the stream.");
                                },
                                Err(E) => {
                                    warn!("OtherSocket: Unable to write data.");
                                    OtherSocketReady.store(false, Ordering::Relaxed);
                                    block_on(d.close());
                                }
                            }
                        }
                        else {
                            if (OtherSocketSendBuf.lock().len() + msg.len() > MaxOtherSocketSendBufSize) {
                                warn! {"Buffer FULL: {} + {} > {}",
                                OtherSocketSendBuf.lock().len(),
                                msg.len(),
                                MaxOtherSocketSendBufSize
                                };
                            } else {
                                debug!{"OtherSocket not ready yet!"};
                                OtherSocketSendBuf.lock().extend_from_slice(&msg);
                            }
                        }
                        Box::pin(async {})
                    }}));
            }
        })
    }));

    debug! {"Successfully registered the on_message handle"};
    let (done_tx, mut done_rx) = tokio::sync::mpsc::channel::<()>(1);
    done_rx.recv().await;
    debug! {"Closing!"};
    RTCDC.close().await.expect("Error closing the connection");

    /* Ok(*/
    (Arc::clone(&RTCDC), OtherSocket) /*)*/
}
async fn configure_send_receive_uds(
    RTCDC: Arc<RTCDataChannel>,
    RTCPC: Arc<RTCPeerConnection>,
    OtherSocket: UnixStream,
) -> (Arc<RTCDataChannel>, UnixStream) /*, Box<dyn error::Error>>*/ {
    // Register channel opening handling
    let d1 = Arc::clone(&RTCDC);
    let mut ClonedSocketRecv = OtherSocket
        .try_clone()
        .expect("Unable to clone the TCP socket. :(");
    let mut ClonedSocketSend = OtherSocket
        .try_clone()
        .expect("Unable to clone the TCP socket. :(");
    RTCPC.on_data_channel(Box::new(move |d: Arc<RTCDataChannel>| {
        let d_label = d.label().to_owned();
        let d_id = d.id();
        info!("New DataChannel {d_label} {d_id}");

        // Register channel opening handling
        Box::pin({
            let d1 = d1.clone();

            let (mut ClonedSocketRecv, mut ClonedSocketSend) = (
                ClonedSocketRecv.try_clone().expect(""),
                ClonedSocketSend.try_clone().expect(""),
                );
            async move {
                let d1 = Arc::clone(&d1);
                let d2 = Arc::clone(&d);
                let d_label2 = d_label.clone();
                let d_id2 = d_id;
                let (mut ClonedSocketRecv, mut ClonedSocketSend) = (
                    ClonedSocketRecv.try_clone().expect(""),
                    ClonedSocketSend.try_clone().expect(""),
                    );
                d.on_open(Box::new({let d1 = d1.clone(); move || {
                    info!("Data channel '{d_label2}'-'{d_id2}' open.");
                    let d1=d1.clone();

                    Box::pin(async move {
                        thread::spawn(move || {
                            let d1 = d1.clone();
                            let (mut ClonedSocketRecv) = (ClonedSocketRecv.try_clone().expect(""));
                            let mut result = Result::<usize>::Ok(0);
                            while result.is_ok() {
                                {
                                    //let mut ready = CAN_RECV.lock(); //.unwrap();
                                    if (CAN_RECV.load(Ordering::Relaxed) == false) {
                                        let mut temp: String = String::new();
                                        println! {"Please press RETURN when you are ready to connect."};
                                        let _ = io::stdin().read_line(&mut temp);
                                        CAN_RECV.store(true, Ordering::Relaxed);
                                    }
                                    //drop(ready);
                                };
                                let d1=d1.clone();
                                let mut buf = [0; 65507];
                                let amt = ClonedSocketRecv
                                    .read(&mut buf)
                                    .expect("Unable to read or save to the buffer");
                                debug! {"{:?}", &buf[0..amt]};
                                let written_bytes = block_on(d2.send(&Bytes::copy_from_slice(&buf[0..amt])))
                                    .expect(&format! {"DataConnection {}: unable to send.", d1.label()});
                                debug!{"Written!"};
                            }});
                    })
                }}));

                // Register text message handling
                d.on_message(Box::new(move |msg: DataChannelMessage| {
                    let (mut ClonedSocketSend) = (ClonedSocketSend.try_clone().expect(""));
                    let msg = msg.data.to_vec();
                    debug!("Message from DataChannel '{d_label}': '{msg:?}'");
                    ClonedSocketSend.write(&msg).expect("Unable to write data.");
                    ClonedSocketSend.flush().expect("Unable to flush the stream.");
                    Box::pin(async {})
                }));
            }
        })
    }));

    debug! {"Successfully registered the on_message handle"};
    let (done_tx, mut done_rx) = tokio::sync::mpsc::channel::<()>(1);
    done_rx.recv().await;
    debug! {"Closing!"};
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
fn main() {
    env_logger::init();
    let mut arg_counter: usize = 0;
    let mut arg: std::env::Args;
    arg = env::args();
    arg_counter = arg.count();
    info!("Arguments received: {arg_counter}");
    if (arg_counter != 2) {
        println! {"Expecting exactly one argument, the TOML file with connection parameters."}
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
    if (config.WebRTCMode == "Accept") {
        let rt = Runtime::new().unwrap();
        let mut offerBase64Text: String = String::new();
        let _ = io::stdin()
            .read_line(&mut offerBase64Text)
            .expect("Cannot read the offer!");
        let offerBase64TextTrimmed = offerBase64Text.trim();
        info! {"Read offer: {}", offerBase64TextTrimmed};
        let offer = decode(&offerBase64TextTrimmed).expect("base64 conversion error");
        let offerRTCSD = serde_json::from_str::<RTCSessionDescription>(&offer)
            .expect("Error parsing the offer!");
        let (mut data_channel, mut peer_connection, mut answer) = rt
            .block_on(accept_WebRTC_offer(offerRTCSD, &config))
            .expect("Failed creating a WebRTC Data Channel.");
        (peer_connection, data_channel) = rt
            .block_on(handle_offer(
                peer_connection,
                data_channel,
                (*answer).clone(),
            ))
            .expect("Error acccepting offer!");
        let ConnectAddress = config
            .Address
            .clone()
            .expect("Binding address not specified");
        let Watchdog = thread::Builder::new()
            .name("Watchdog".to_string())
            .spawn(move || {
                debug! {"Inactivity monitoring watchdog has started"}
                loop {
                    let five_seconds = time::Duration::from_secs(60);
                    debug! {"Watchdog will sleep for five seconds."};
                    let current_time : u64 = chrono::Utc::now().timestamp().try_into().expect(
                        "This software is not supposed to be used before UNIX was invented.",
                        );
                    debug!{
                        "Stream was last active {} seconds ago. The current time is: {}. Last active time: {}.", 
                        current_time - STREAM_LAST_ACTIVE_TIME.load(Ordering::Relaxed),
                        current_time,
                        STREAM_LAST_ACTIVE_TIME.load(Ordering::Relaxed)
                    };
                    for thread in Threads.lock().iter(){
                        if (thread.is_finished()){
                            info!{"Done, dead or killed: {:?}", thread};
                        }
                        else{
                            debug!{"Alive: {:?}", thread};
                        }
                    }
                    thread::sleep(five_seconds);
                    debug! {"Watchdog: Resuming..."};
                }
            });
        let mut buf = [0; 65507];
        if (config.Type == "UDP") {
            info! {"UDP socket requested"};
            let ConnectPort = config.Port.clone().expect("Connecting port not specified");
            info! {"Connecting to UDP to address {} port {}", ConnectAddress, ConnectPort};
            let mut OtherSocket = UdpSocket::bind(format! {"{}:{}", ConnectAddress, ConnectPort})
                .expect(&format! {
                    "Could not connect to UDP port: {}:{}", &ConnectAddress, &ConnectPort
                });
            info! {"Connected UDP on address {} port {}", ConnectAddress, ConnectPort};
            OtherSocket
                .connect(format!("{}:{}", ConnectAddress, ConnectPort))
                .expect(&format! {"UDP connect error: connect() to {}", format!{
                    "{}:{}", ConnectAddress, ConnectPort
                }});
            STREAM_LAST_ACTIVE_TIME.store(
                chrono::Utc::now()
                    .timestamp()
                    .try_into()
                    .expect("This software is not supposed to be used before UNIX was invented."),
                Ordering::Relaxed,
            );
            (data_channel, OtherSocket) = rt.block_on(configure_send_receive_udp(
                data_channel,
                peer_connection,
                OtherSocket,
            ));
        } else if (config.Type == "TCP") {
            info! {"TCP socket requested"};
            let ConnectPort = config.Port.clone().expect("Connecting port not specified");
            info! {"Connecting TCP on address {} port {}", ConnectAddress, ConnectPort};
            let mut OtherSocket = TcpStream::connect(format!("{}:{}", ConnectAddress, ConnectPort))
                .expect("Error getting the TCP stream");
            debug! {"Attempting to write the send buffer: {:?}", &OtherSocketSendBuf.lock()};
            OtherSocket.write(&OtherSocketSendBuf.lock());
            info! {"Connected to TCP: address {} port {}", ConnectAddress, ConnectPort};
            match (OtherSocket.set_nodelay(true)) {
                Ok(_) => debug! {"NODELAY set"},
                Err(_) => warn!("SO_NODELAY failed."),
            }
            STREAM_LAST_ACTIVE_TIME.store(
                chrono::Utc::now()
                    .timestamp()
                    .try_into()
                    .expect("This software is not supposed to be used before UNIX was invented."),
                Ordering::Relaxed,
            );
            (data_channel, OtherSocket) = rt.block_on(configure_send_receive_tcp(
                data_channel,
                peer_connection,
                OtherSocket,
            ));
        } else if (config.Type == "UDS") {
            info! {"Unix Domain Socket requested."};
            let mut OtherSocket = UnixStream::connect(ConnectAddress).expect("UDS connect error");
            (data_channel, OtherSocket) = rt.block_on(configure_send_receive_uds(
                data_channel,
                peer_connection,
                OtherSocket,
            ));
        }
    }
}
