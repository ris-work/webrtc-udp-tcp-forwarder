use log::{info};
use std::env;
use std::fs::read_to_string;
use std::process::exit;
use serde::Deserialize;
#[derive(Deserialize)]
struct Config {
    Type: String,
    WebRTCMode: String,
    Address: Option<String>,
    Port: Option<String>,
    ICEServers: Vec<String>
}
#[derive(Deserialize)]
struct ICEServer {
    URL: String,
    Username: Option<String>,
    Credential: Option<String>
}
fn main() {
    let mut arg_counter: usize = 0;
    let mut arg: std::env::Args;
    arg = env::args();
    arg_counter = arg.count();
    info!("Arguments received: {arg_counter}");
    if (arg_counter != 2) {
        println!{"Expecting exactly one argument, the TOML file with connection parameters."}
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
    println!("Hello, world!");
}
