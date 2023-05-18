#![allow(non_snake_case)]
#![allow(unused_parens)]
use anyhow::Result;
use futures::executor::block_on;
use log::{debug, error, info, warn};
use serde::Deserialize;
use std::env;
use std::error;
use std::fmt;
use std::fs::read_to_string;
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
    base64::encode(b)
}
pub fn decode(s: &str) -> Result<String> {
    let b = base64::decode(s)?;
    let s = String::from_utf8(b)?;
    Ok(s)
}
fn handle_TCP_client(stream: TcpStream) {}
async fn create_WebRTC_offer(config: Config) -> Result<Arc<RTCDataChannel>, Box<dyn error::Error>> {
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
            urls: config.ICEServers,
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

    // Register channel opening handling
    let d1 = Arc::clone(&data_channel);
    data_channel.on_open(Box::new(move || {
        info!("Data channel '{}'-'{}' open. Random messages will now be sent to any connected DataChannels every 5 seconds", d1.label(), d1.id());

        let d2 = Arc::clone(&d1);
        Box::pin(async move {
            let mut result = Result::<usize>::Ok(0);
            while result.is_ok() {
                let timeout = tokio::time::sleep(Duration::from_secs(5));
                tokio::pin!(timeout);

                tokio::select! {
                    _ = timeout.as_mut() =>{
                        let message = math_rand_alpha(15);
                        println!("Sending '{message}'");
                        result = d2.send_text(message).await.map_err(Into::into);
                    }
                };
            }
        })
    }));

    // Register text message handling
    let d_label = data_channel.label().to_owned();
    data_channel.on_message(Box::new(move |msg: DataChannelMessage| {
        let msg_str = String::from_utf8(msg.data.to_vec()).unwrap();
        info!("Message from DataChannel '{d_label}': '{msg_str}'");
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
    } else {
        info!("generate local_description failed!");
    }
    Ok(Arc::clone(&data_channel))
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
            let OtherSocket = UdpSocket::bind(format! {"{}:{}", BindAddress, BindPort})
                .expect(&format! {"Could not bind to UDP port: {}:{}", &BindAddress, &BindPort});
            info! {"Bound UDP on address {} port {}", BindAddress, BindPort};
            let (amt, src) = OtherSocket
                .recv_from(&mut buf)
                .expect("Error saving to buffer");
            debug!("{:?}", buf);
            OtherSocket.send_to(&buf, &src).expect("UDP: Write failed!");
            let rt = Runtime::new().unwrap();
            rt.block_on(create_WebRTC_offer(config)).expect("Failed creating a WebRTC Data Channel.");
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
            OtherSocket
                .read(&mut buf)
                .expect("TCP Stream: Read failed!");
            debug!("{:?}", buf);
            OtherSocket.write(&buf).expect("TCP Stream: Write failed!");
            let rt = Runtime::new().unwrap();
            rt.block_on(create_WebRTC_offer(config)).expect("Failed creating a WebRTC Data Channel.");
        } else if (config.Type == "UDS") {
            info! {"Unix Domain Socket requested."};
            let Listener = UnixListener::bind(BindAddress);
            let mut OtherSocket = Listener
                .expect("UDS listen error")
                .incoming()
                .next()
                .expect("Error getting the UDS stream")
                .expect("UDS stream error");
            OtherSocket
                .read(&mut buf)
                .expect("UnixStream: Read failed!");
            debug!("{:?}", buf);
            OtherSocket.write(&buf).expect("UnixStream: Write failed!");
        }
    }
}
