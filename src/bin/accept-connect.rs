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
use std::io::Result as IOResult;
use std::io::Write;
use std::io::{BufRead, BufReader};

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

use std::future::Future;
use std::path::Path;
use std::pin::Pin;
use std::task::{Context, Poll};

use tokio::net::UdpSocket as TokioUdpSocket;
use tokio::runtime::Handle;
use tokio::sync::Semaphore;

use mimalloc::MiMalloc;

#[global_allocator]
static GLOBAL: MiMalloc = MiMalloc;

static STREAM_LAST_ACTIVE_TIME: AtomicU64 = AtomicU64::new(0);
static OtherSocketReady: AtomicBool = AtomicBool::new(false);
static DataChannelReady: AtomicBool = AtomicBool::new(false);
static CAN_RECV: AtomicBool = AtomicBool::new(true);
static MaxOtherSocketSendBufSize: usize = 2048;
static THREAD_STACK_SIZE: usize = 204800;
const PKT_SIZE: usize = 16384;

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
    ICEServers: Vec<ICEServer>,
    Ordered: Option<bool>,
    ConHost: Option<bool>,
}
#[derive(Deserialize, Clone)]
struct ICEServer {
    URLs: Vec<String>,
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
    let mut ice_servers: Vec<RTCIceServer> = vec![];
    for ICEServer in config.ICEServers.iter() {
        match (&ICEServer.Username) {
            Some(Username) => ice_servers.push(RTCIceServer {
                urls: ICEServer.URLs.clone(),
                username: Username.clone(),
                credential: ICEServer
                    .Credential
                    .clone()
                    .expect("Empty credentials are not allowed."),
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
    let data_channel = peer_connection
        .create_data_channel(
            "data",
            Some(RTCDataChannelInit {
                ordered: Some(false),
                max_packet_life_time: None,
                max_retransmits: None,
                protocol: Some("raw".to_string()),
                negotiated: None,
            }),
        )
        .await?;

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
) -> (Arc<RTCDataChannel>, Arc<TokioUdpSocket>) /*, Box<dyn error::Error>>*/ {
    // Register channel opening handling
    OtherSocket
        .set_nonblocking(true)
        .expect("Cannot enter non-blocking UDP mode.");
    let OtherSocket =
        TokioUdpSocket::from_std(OtherSocket).expect("Unable to form a Tokio UDP Socket.");

    let d1 = Arc::clone(&RTCDC);
    let TOS = Arc::new(OtherSocket);
    let mut ClonedSocketRecv = TOS.clone();
    //.expect("Unable to clone the TCP socket. :(");
    let mut ClonedSocketSend = TOS.clone();
    //.expect("Unable to clone the TCP socket. :(");
    RTCPC.on_data_channel(Box::new(move |d: Arc<RTCDataChannel>| {
        let d_label = d.label().to_owned();
        let d_id = d.id();
        info!("New DataChannel {d_label} {d_id}");
        let rt=Handle::current();
                        let art = Arc::new(rt);

        // Register channel opening handling
        Box::pin({
            let d1 = d1.clone();

            let (mut ClonedSocketRecv, mut ClonedSocketSend) = (
                ClonedSocketRecv.clone(),
                ClonedSocketSend.clone(),
                );
            async move {
                let d1 = Arc::clone(&d1);
                let d2 = Arc::clone(&d);
                let art=art.clone();
                let d_label2 = d_label.clone();
                let d_id2 = d_id;
                let (mut ClonedSocketRecv, mut ClonedSocketSend) = (
                    ClonedSocketRecv.clone(),
                    ClonedSocketSend.clone(),
                    );
                d.on_open(Box::new({let d1 = d1.clone(); let rt=art.clone(); move || {

                    info!("Data channel '{d_label2}'-'{d_id2}' open.");
                    let d1=d1.clone();
                    let spawned = thread::Builder::new()
                        .name("OS->DC".to_string())
                        .stack_size(THREAD_STACK_SIZE)
                        .spawn(move || {
                        info!{"Spawned the thread: OtherSocket (read) => DataChannel (write)"};
                        
                        let d1 = d1.clone();
                        let (mut ClonedSocketRecv) = (ClonedSocketRecv.clone());
                        rt.spawn(
                        async move {loop {
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
                            let mut buf = [0; PKT_SIZE];
                            let amt = ClonedSocketRecv
                                .recv(&mut buf).await;
                            match (amt){

                                Ok(amt) => {
                                    trace! {"{:?}", &buf[0..amt]};
                                    debug!{"Blocking on DC send..."};
                                    let written_bytes = d2.send(&Bytes::copy_from_slice(&buf[0..amt])).await;
                                    match(written_bytes) {
                                        Ok(Bytes) => {debug!{"OS->DC: Written {Bytes} bytes!"};},
                                        #[cold] Err(E) => {
                                            info!{"DataConnection {}: unable to send: {:?}.",
                                            d1.label(),
                                            E};
                                            info!{"Breaking the loop due to previous error: OtherSocket (read) => DataChannel (write)"};
                                            break;
                                        }
                                    }
                                },
                                #[cold] Err(E) => {
                                    info!{"OtherSocket: Connection closed."};
                                    info!{"{:?}", E};
                                    info!{"Breaking the loop due to previous error: OtherSocket (read) => DataChannel (write)"};
                                    break;
                                }
                            }
                    }})});
                    match(spawned){
                        Ok(JH)=>{},//Threads.lock().push(JH)},
                        Err(E) =>{error!{"Unable to spawn: {:?}", E}} 
                    }

                    Box::pin(async move {
                    })
                }}));

                
                // Register text message handling
                d.on_message(Box::new({let d=d.clone(); let art=art.clone();
                    move |msg: DataChannelMessage| {
                        let d = d.clone();
                        let d_label = d_label.clone();
                        let ClonedSocketSend = ClonedSocketSend.clone();
                        art.spawn(
                            
                        async move {
                        let msg = msg.data.to_vec();
                        trace!("Message from DataChannel '{d_label}': '{msg:?}'");
                        if (CAN_RECV.load(Ordering::Relaxed)){
                            let (mut ClonedSocketSend) = (ClonedSocketSend.clone());
                            match(
                                ClonedSocketSend.send(&msg).await
                                )
                            {
                                Ok(amt) => {
                                    debug!{"DC->OS: Written {} bytes.", amt};
                                    //ClonedSocketSend.flush().expect("Unable to flush the stream.");
                                },
                                #[cold] Err(E) => {
                                    warn!("OtherSocket: Unable to write data.");
                                    OtherSocketReady.store(false, Ordering::Relaxed);
                                    block_on(d.close());
                                }
                            }
                        }
                        //#[cold] 
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
                    });
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
    (Arc::clone(&RTCDC), TOS) /*)*/
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
                    let spawned = thread::Builder::new()
                        .name("OS->DC".to_string())
                        .stack_size(THREAD_STACK_SIZE)
                        .spawn(move || {
                        info!{"Spawned the thread: OtherSocket (read) => DataChannel (write)"};
                        let rt=Builder::new_multi_thread()
                            .worker_threads(1)
                            .thread_name("TOKIO: OS->DC")
                            .build()
                            .unwrap();
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
                            let mut buf = [0; PKT_SIZE];
                            let amt = ClonedSocketRecv
                                .read(&mut buf);
                            match (amt){

                                Ok(amt) => {
                                    trace! {"{:?}", &buf[0..amt]};
                                    debug!{"Blocking on DC send..."};
                                    let written_bytes = rt.block_on(d2.send(&Bytes::copy_from_slice(&buf[0..amt])));
                                    match(written_bytes) {
                                        Ok(Bytes) => {debug!{"OS->DC: Written {Bytes} bytes!"};},
                                        #[cold] Err(E) => {
                                            info!{"DataConnection {}: unable to send: {:?}.",
                                            d1.label(),
                                            E};
                                            info!{"Breaking the loop due to previous error: OtherSocket (read) => DataChannel (write)"};
                                            break;
                                        }
                                    }
                                },
                                #[cold] Err(E) => {
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
                                    debug!{"DC->OS: Written {} bytes.", amt};
                                    ClonedSocketSend.flush().expect("Unable to flush the stream.");
                                },
                                #[cold] Err(E) => {
                                    warn!("OtherSocket: Unable to write data.");
                                    OtherSocketReady.store(false, Ordering::Relaxed);
                                    block_on(d.close());
                                }
                            }
                        }
                        // #[cold] 
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
                    let spawned = thread::Builder::new()
                        .name("OS->DC".to_string())
                        .stack_size(THREAD_STACK_SIZE)
                        .spawn(move || {
                        info!{"Spawned the thread: OtherSocket (read) => DataChannel (write)"};
                        let rt=Builder::new_multi_thread()
                            .worker_threads(1)
                            .thread_name("TOKIO: OS->DC")
                            .build()
                            .unwrap();
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
                            let mut buf = [0; PKT_SIZE];
                            let amt = ClonedSocketRecv
                                .read(&mut buf);
                            match (amt){

                                Ok(amt) => {
                                    trace! {"{:?}", &buf[0..amt]};
                                    debug!{"Blocking on DC send..."};
                                    let written_bytes = rt.block_on(d2.send(&Bytes::copy_from_slice(&buf[0..amt])));
                                    match(written_bytes) {
                                        Ok(Bytes) => {debug!{"OS->DC: Written {Bytes} bytes!"};},
                                        #[cold] Err(E) => {
                                            info!{"DataConnection {}: unable to send: {:?}.",
                                            d1.label(),
                                            E};
                                            info!{"Breaking the loop due to previous error: OtherSocket (read) => DataChannel (write)"};
                                            break;
                                        }
                                    }
                                },
                                #[cold] Err(E) => {
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
                                    debug!{"DC->OS: Written {} bytes.", amt};
                                    ClonedSocketSend.flush().expect("Unable to flush the stream.");
                                },
                                #[cold] Err(E) => {
                                    warn!("OtherSocket: Unable to write data.");
                                    OtherSocketReady.store(false, Ordering::Relaxed);
                                    block_on(d.close());
                                }
                            }
                        }
                        // #[cold] 
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
        //let rt = Runtime::new().unwrap();
        let rt = Builder::new_current_thread()
            .enable_all()
            .thread_name("TOKIO: main")
            .build()
            .unwrap();
        let mut offerBase64Text: String = String::new();
        match (config.ConHost) {
            Some(val) => {
                if (val == true) {
                    let mut line: String;
                    let mut buf: Vec<u8> = vec![];
                    BufReader::new(io::stdin()).read_until(b'/', &mut buf);
                    let lines: String =
                        String::from(std::str::from_utf8(&buf).expect("Input not UTF-8"));
                    line = String::from(lines.replace("\n", "").replace("\r", "").replace(" ", ""));
                    let _ = line.pop();
                    offerBase64Text = line;
                } else {
                    let _ = io::stdin()
                        .read_line(&mut offerBase64Text)
                        .expect("Cannot read the offer!");
                }
            }
            None => {
                let _ = io::stdin()
                    .read_line(&mut offerBase64Text)
                    .expect("Cannot read the offer!");
            }
        }
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
                    let five_seconds = time::Duration::from_secs(600);
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
        let mut buf = [0; PKT_SIZE];
        if (config.Type == "UDP") {
            info! {"UDP socket requested"};
            let ConnectPort = config.Port.clone().expect("Connecting port not specified");
            info! {"Connecting to UDP to address {} port {}", ConnectAddress, ConnectPort};
            let mut OtherSocket =
                UdpSocket::bind("0.0.0.0:0").expect("UDP Socket: unable to bind: 0.0.0.0:0.");
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
            STREAM_LAST_ACTIVE_TIME.store(
                chrono::Utc::now()
                    .timestamp()
                    .try_into()
                    .expect("This software is not supposed to be used before UNIX was invented."),
                Ordering::Relaxed,
            );
            let TOS: Arc<TokioUdpSocket>;
            (data_channel, TOS) = rt.block_on(configure_send_receive_udp(
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
        } else {
            println! {"Unsupported type: {}", config.Type};
        }
    } else {
        println! {"Unsupported WebRTC Mode: {}. Probably the WRONG TOOL.", config.WebRTCMode};
    }
}
