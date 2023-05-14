use log::{info};
use std::env;
use std::fs::read_to_string;
use std::process::exit;
use serde::Deserialize;
use std::net::UdpSocket;
use std::net::TcpStream;
#[derive(Deserialize)]
struct Config {
    Type: String,
    WebRTCMode: String,
    Address: Option<String>,
    Port: Option<String>,
    ICEServers: Vec<String>,
    Ordered: Option<bool>
}
#[derive(Deserialize)]
struct ICEServer {
    URL: String,
    Username: Option<String>,
    Credential: Option<String>
}
#[derive(Deserialize)]
struct WebRTC_Status {
    Status: String,
    SDP_Params: Option<String>,
    SRTP_Params: Option<String>
}
fn main() {
    env_logger::init();
    let mut arg_counter: usize = 0;
    let mut arg: std::env::Args;
    arg = env::args();
    arg_counter = arg.count();
    info!("Arguments received: {arg_counter}");
    if (arg_counter != 2) {
        println!{"Expecting exactly one argument, the TOML file with connection parameters."}
        exit(2);
    }
    let TOML_file_name = env::args().nth(1).unwrap();
    info!{"Reading from: {}", TOML_file_name};
    let TOML_file_read = read_to_string(TOML_file_name);
    let TOML_file_contents: String;
    match TOML_file_read {
        Ok (T) => TOML_file_contents = T,
        Err (E) => exit(1)
    }
    info!{"{}", TOML_file_contents};
    let config : Config = toml::from_str(&TOML_file_contents).unwrap();
    info!("Configuration: type: {}", config.Type);
    if(config.Type=="UDP"){
        info!{"UDP socket requested"};
    }
}
