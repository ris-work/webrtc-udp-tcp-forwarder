use log::info;
use serde::Deserialize;
use std::env;
use std::fmt;
use std::fs::read_to_string;
use std::net::TcpListener;
use std::net::TcpStream;
use std::net::UdpSocket;
use std::process::exit;
use std::io::Write;
use std::io::Read;
#[cfg(unix)]
use std::os::unix::net::UnixStream;
#[cfg(windows)]
use uds_windows::stdnet::socket;
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
fn handle_TCP_client(stream: TcpStream) {}
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
        Err(E) => exit(1),
    }
    info! {"{}", TOML_file_contents};
    let config: Config = toml::from_str(&TOML_file_contents).unwrap();
    info!("Configuration: type: {}", config.Type);
    if (config.WebRTCMode == "Offer") {
        let BindAddress = config
            .Address
            .clone()
            .expect("Binding address not specified");
        let BindPort = config.Port.clone().expect("Binding port not specified");
        let mut buf = [0; 10];
        let SendBytes = "Hello!".bytes();
        if (config.Type == "UDP") {
            info! {"UDP socket requested"};
            info! {"Binding UDP on address {} port {}", BindAddress, BindPort};
            let OtherSocket = UdpSocket::bind(format! {"{}:{}", BindAddress, BindPort})
                .expect(&format! {"Could not bind to UDP port: {}:{}", &BindAddress, &BindPort});
            info! {"Bound UDP on address {} port {}", BindAddress, BindPort};
            let (amt, src) = OtherSocket
                .recv_from(&mut buf)
                .expect("Error saving to buffer");
            OtherSocket.send_to(&buf, &src);
        } else if (config.Type == "TCP") {
            info! {"TCP socket requested"};
            info! {"Binding TCP on address {} port {}", BindAddress, BindPort};
            let Listener = TcpListener::bind(format! {"{}:{}", BindAddress, BindPort})
                .expect(&format! {"Could not bind to TCP port: {}:{}", &BindAddress, &BindPort});
            info! {"Bound TCP on address {} port {}", BindAddress, BindPort};
            let mut OtherSocket = Listener
                .incoming()
                .next()
                .expect("Error getting the TCP stream")
                .expect("TCP stream error");
            OtherSocket.read(&mut buf);
            OtherSocket.write(&buf);
        }
    }
}
