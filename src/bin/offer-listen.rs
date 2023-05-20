#![allow(non_snake_case)]
#![allow(unused_parens)]
use anyhow::Result;
use base64::engine::{self, general_purpose};
use base64::Engine;
use bytes::Bytes;
use futures::executor::block_on;
use log::{debug, error, info, warn};
use serde::Deserialize;
use std::env;
use std::error;
use std::fmt;
use std::fs;
use std::fs::read_to_string;
use std::io;
use std::io::Read;
use std::io::Write;
use std::net::TcpListener;
use std::net::TcpStream;
use std::net::UdpSocket;
#[cfg(unix)]
use std::os::unix::net::{UnixListener, UnixStream};
use std::process::exit;
use std::sync::Arc;
use tokio::runtime::Runtime;
use tokio::time::Duration;
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
use webrtc::peer_connection::math_rand_alpha;
use webrtc::peer_connection::peer_connection_state::RTCPeerConnectionState;
use webrtc::peer_connection::sdp::session_description::RTCSessionDescription;
use webrtc::peer_connection::RTCPeerConnection;

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
async fn create_WebRTC_offer(
    config: &Config,
) -> Result<(Arc<RTCDataChannel>, Arc<RTCPeerConnection>), Box<dyn error::Error>> {
    // Create a MediaEngine object to configure the supported codec
    let mut m = MediaEngine::default();

    // Register default codecs
    m.register_default_codecs();

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

    // Output the answer in base64 so we can paste it in browser
    if let Some(local_desc) = peer_connection.local_description().await {
        let json_str = serde_json::to_string(&local_desc)?;
        //let b64 = encode(&json_str);
        info!("{json_str}");
        let b64 = encode(&json_str);
        info!("{b64}");
    } else {
        info!("generate local_description failed!");
    }
    Ok((Arc::clone(&data_channel), peer_connection))
}
async fn configure_send_receive_udp(
    RTCDC: Arc<RTCDataChannel>,
    OtherSocket: UdpSocket,
) -> (Arc<RTCDataChannel>, UdpSocket) /*, Box<dyn error::Error>>*/ {
    // Register channel opening handling
    let d1 = Arc::clone(&RTCDC);
    let ClonedSocketRecv = OtherSocket
        .try_clone()
        .expect("Unable to clone the UDP socket. :(");
    let ClonedSocketSend = OtherSocket
        .try_clone()
        .expect("Unable to clone the UDP socket. :(");
    RTCDC.on_open(Box::new(move || {
        info!("Data channel '{}'-'{}' open.", d1.label(), d1.id());
        let d2 = Arc::clone(&d1);

        Box::pin(async move {
            let mut result = Result::<usize>::Ok(0);
            while result.is_ok() {
                let mut buf = [0; 65507];
                let mut amt = ClonedSocketRecv
                    .recv(&mut buf)
                    .expect("Unable to save to buffer");
                debug! {"{:?}", &buf[0..amt]};
                d2.send(&Bytes::copy_from_slice(&buf[0..amt])).await;
            }
        })
    }));

    // Register text message handling
    let d_label = RTCDC.label().to_owned();
    RTCDC.on_message(Box::new(move |msg: DataChannelMessage| {
        let msg = msg.data.to_vec();
        debug!("Message from DataChannel '{d_label}': '{msg:?}'");
        ClonedSocketSend.send(&msg);
        Box::pin(async {})
    }));
    let (done_tx, mut done_rx) = tokio::sync::mpsc::channel::<()>(1);
    done_rx.recv().await;
    debug! {"Closing!"};
    RTCDC.close().await.expect("Error closing the connection");

    /* Ok(*/
    (Arc::clone(&RTCDC), OtherSocket) /*)*/
}
async fn configure_send_receive_tcp(
    RTCDC: Arc<RTCDataChannel>,
    OtherSocket: TcpStream,
) -> (Arc<RTCDataChannel>, TcpStream) /*, Box<dyn error::Error>>*/ {
    // Register channel opening handling
    let d1 = Arc::clone(&RTCDC);
    let mut ClonedSocketRecv = OtherSocket
        .try_clone()
        .expect("Unable to clone the UDP socket. :(");
    let mut ClonedSocketSend = OtherSocket
        .try_clone()
        .expect("Unable to clone the UDP socket. :(");
    RTCDC.on_open(Box::new(move || {
        info!("Data channel '{}'-'{}' open.", d1.label(), d1.id());
        let d2 = Arc::clone(&d1);

        Box::pin(async move {
            let mut result = Result::<usize>::Ok(0);
            while result.is_ok() {
                let mut buf = [0; 65507];
                let mut amt = ClonedSocketRecv
                    .read(&mut buf)
                    .expect("Unable to save to buffer");
                debug! {"{:?}", &buf[0..amt]};
                d2.send(&Bytes::copy_from_slice(&buf[0..amt])).await;
            }
        })
    }));

    // Register text message handling
    let d_label = RTCDC.label().to_owned();
    RTCDC.on_message(Box::new(move |msg: DataChannelMessage| {
        let msg = msg.data.to_vec();
        debug!("Message from DataChannel '{d_label}': '{msg:?}'");
        ClonedSocketSend.write(&msg);
        Box::pin(async {})
    }));
    let (done_tx, mut done_rx) = tokio::sync::mpsc::channel::<()>(1);
    done_rx.recv().await;
    debug! {"Closing!"};
    RTCDC.close().await.expect("Error closing the connection");

    /* Ok(*/
    (Arc::clone(&RTCDC), OtherSocket) /*)*/
}
async fn configure_send_receive_uds(
    RTCDC: Arc<RTCDataChannel>,
    OtherSocket: UnixStream,
) -> (Arc<RTCDataChannel>, UnixStream) /*, Box<dyn error::Error>>*/ {
    // Register channel opening handling
    let d1 = Arc::clone(&RTCDC);
    let mut ClonedSocketRecv = OtherSocket
        .try_clone()
        .expect("Unable to clone the UDP socket. :(");
    let mut ClonedSocketSend = OtherSocket
        .try_clone()
        .expect("Unable to clone the UDP socket. :(");
    RTCDC.on_open(Box::new(move || {
        info!("Data channel '{}'-'{}' open.", d1.label(), d1.id());
        let d2 = Arc::clone(&d1);

        Box::pin(async move {
            let mut result = Result::<usize>::Ok(0);
            while result.is_ok() {
                let mut buf = [0; 65507];
                let mut amt = ClonedSocketRecv
                    .read(&mut buf)
                    .expect("Unable to save to buffer");
                debug! {"{:?}", &buf[0..amt]};
                d2.send(&Bytes::copy_from_slice(&buf[0..amt])).await;
            }
        })
    }));

    // Register text message handling
    let d_label = RTCDC.label().to_owned();
    RTCDC.on_message(Box::new(move |msg: DataChannelMessage| {
        let msg = msg.data.to_vec();
        debug!("Message from DataChannel '{d_label}': '{msg:?}'");
        ClonedSocketSend.write(&msg);
        Box::pin(async {})
    }));
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
    conn.set_remote_description(session_description).await?;
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
    if (config.WebRTCMode == "Offer") {
        let rt = Runtime::new().unwrap();
        let (mut data_channel, mut peer_connection) = rt
            .block_on(create_WebRTC_offer(&config))
            .expect("Failed creating a WebRTC Data Channel.");
        let _ = io::stdin().read(&mut [0u8]).unwrap();
        let offerBase64Text = &fs::read_to_string("offer.txt").expect("Cannot read the offer!");
        info! {"Read offer: {}", offerBase64Text};
        let offer = decode(&offerBase64Text).expect("base64 conversion error");
        let answer = serde_json::from_str::<RTCSessionDescription>(&offer)
            .expect("Error parsing the offer!");
        (peer_connection, data_channel) = rt
            .block_on(handle_offer(peer_connection, data_channel, answer))
            .expect("Error acccepting offer!");
        let BindAddress = config
            .Address
            .clone()
            .expect("Binding address not specified");
        let mut buf = [0; 65507];
        let SendBytes = "Hello!".bytes();
        if (config.Type == "UDP") {
            info! {"UDP socket requested"};
            let BindPort = config.Port.clone().expect("Binding port not specified");
            info! {"Binding UDP on address {} port {}", BindAddress, BindPort};
            let mut OtherSocket = UdpSocket::bind(format! {"{}:{}", BindAddress, BindPort})
                .expect(&format! {"Could not bind to UDP port: {}:{}", &BindAddress, &BindPort});
            info! {"Bound UDP on address {} port {}", BindAddress, BindPort};
            let (amt, src) = OtherSocket
                .peek_from(&mut buf)
                .expect("Error saving to buffer");
            info!("UDP connecting to: {}", src);
            OtherSocket.connect(src);
            (data_channel, OtherSocket) =
                rt.block_on(configure_send_receive_udp(data_channel, OtherSocket));
        } else if (config.Type == "TCP") {
            info! {"TCP socket requested"};
            let BindPort = config.Port.clone().expect("Binding port not specified");
            info! {"Binding TCP on address {} port {}", BindAddress, BindPort};
            let Listener = TcpListener::bind(format! {"{}:{}", BindAddress, BindPort})
                .expect(&format! {"Could not bind to TCP port: {}:{}", &BindAddress, &BindPort});
            info! {"Bound TCP on address {} port {}", BindAddress, BindPort};
            let mut OtherSocket = Listener
                .incoming()
                .next()
                .expect("Error getting the TCP stream")
                .expect("TCP stream error");
            (data_channel, OtherSocket) =
                rt.block_on(configure_send_receive_tcp(data_channel, OtherSocket));
        } else if (config.Type == "UDS") {
            info! {"Unix Domain Socket requested."};
            let Listener = UnixListener::bind(BindAddress);
            let mut OtherSocket = Listener
                .expect("UDS listen error")
                .incoming()
                .next()
                .expect("Error getting the UDS stream")
                .expect("UDS stream error");
            (data_channel, OtherSocket) =
                rt.block_on(configure_send_receive_uds(data_channel, OtherSocket));
        }
    }
}