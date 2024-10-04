#![allow(non_snake_case)]
#![allow(unused_parens)]
#![allow(unused_assignments)]
#![allow(non_camel_case_types)]
#![allow(dead_code)]
#![allow(unused_mut)]
#![allow(unused_variables)]
#![allow(non_upper_case_globals)]
use chrono::naive::NaiveDateTime;
use chrono::Utc;
use core_affinity;
use serde::Deserialize;
use std::net::TcpStream;
#[cfg(unix)]
use std::os::unix::net::{UnixListener, UnixStream};
#[cfg(windows)]
use uds_windows::{UnixListener, UnixStream};
use std::io::{Read, Write};

pub const PKT_SIZE: u16 = 2046;
#[derive(Deserialize, Clone)]
pub struct Config {
    pub Type: String,
    pub WebRTCMode: String,
    pub Address: Option<String>,
    pub Port: Option<String>,
    pub ICEServers: Vec<ICEServer>,
    pub Ordered: Option<bool>,
    pub ConHost: Option<bool>,
    pub Publish: Option<bool>,
    pub PublishType: Option<String>,
    pub PublishEndpoint: Option<String>,
    pub PublishAuthType: Option<String>,
    pub PublishAuthUser: Option<String>,
    pub PublishAuthPass: Option<String>,
    pub PeerAuthType: Option<String>,
    pub PeerPSK: Option<String>,
    pub nsTimeout: Option<u64>, //TODO
    pub sTimeTolerance: Option<u64>,
    pub TimeoutCountMax: Option<u64>,
    pub PinnedCores: Option<[usize; 4]>,
    //[tokio WebRTC receiver -> queue, queue -> tokio WebRTC send,
    // OS -> queue, queue -> OS]
    pub ICEListenAddressPort: Option<String>,
}
#[derive(Deserialize, Clone)]
pub struct ICEServer {
    pub URLs: Vec<String>,
    pub Username: Option<String>,
    pub Credential: Option<String>,
}
pub mod hmac {
    use crate::message::TimedMessage;
    use crate::Config;
    use hex::{decode, encode};
    use hmac::{Hmac, Mac};
    use serde::{Deserialize, Serialize};
    use serde_json::Result;
    use sha2::Sha256;
    use std::error::Error;
    use std::fmt;
    use std::fmt::Result as fResult;
    use std::result::Result as stdResult;
    #[derive(Debug, Clone)]
    pub struct HMACVerificationFailed;
    impl fmt::Display for HMACVerificationFailed {
        fn fmt(&self, f: &mut fmt::Formatter) -> fResult {
            write! {f, "Message too old or new"}
        }
    }
    impl Error for HMACVerificationFailed {}
    #[derive(Deserialize, Clone, Serialize)]
    pub struct HashAuthenticatedMessage {
        pub MessageWithTime: String,
        pub MAC: String,
    }
    pub fn ConstructAuthenticatedMessage(
        Timed: TimedMessage,
        config: Config,
    ) -> HashAuthenticatedMessage {
        let SerializedMessage: String =
            serde_json::to_string(&Timed).expect("Unable to serialize.");
        let mut MacGen = Hmac::<Sha256>::new_from_slice(
            config.PeerPSK.expect("No peer PSK provided.").as_bytes(),
        )
        .expect("Unable to load the MAC generator.");
        MacGen.update(SerializedMessage.as_bytes());
        let Mac = MacGen.finalize();
        HashAuthenticatedMessage {
            MessageWithTime: SerializedMessage,
            MAC: String::from(hex::encode(Mac.into_bytes())),
        }
    }
    pub fn VerifyAndReturn(
        Msg: HashAuthenticatedMessage,
        config: Config,
    ) -> stdResult<TimedMessage, Box<dyn Error>> {
        let MAC = hex::decode(Msg.MAC)?;
        let mut MacGen = Hmac::<Sha256>::new_from_slice(
            config.PeerPSK.expect("No peer PSK provided.").as_bytes(),
        )?;
        MacGen.update(Msg.MessageWithTime.as_bytes());
        MacGen.verify_slice(&MAC[..])?;
        let Timed: TimedMessage = serde_json::from_str(&Msg.MessageWithTime)?;
        Ok(Timed)
    }
}
pub mod message {
    use chrono::naive::NaiveDateTime;
    use chrono::Utc;
    use serde::{Deserialize, Serialize};
    use std::error::Error;
    use std::fmt;
    use std::fmt::Result;
    use std::result::Result as stdResult;
    #[derive(Debug, Clone)]
    pub struct MessageTooOldOrTooNewError;
    impl fmt::Display for MessageTooOldOrTooNewError {
        fn fmt(&self, f: &mut fmt::Formatter) -> Result {
            write! {f, "Message too old or new"}
        }
    }
    impl Error for MessageTooOldOrTooNewError {}
    #[derive(Deserialize, Clone, Serialize)]
    pub struct TimedMessage {
        Timestamp: String,
        Message: String,
    }
    pub fn ConstructMessage(
        Message: String,
        config: crate::Config,
    ) -> TimedMessage {
        return TimedMessage {
            Timestamp: Utc::now().naive_utc().timestamp_micros().to_string(),
            Message: Message,
        };
    }
    pub fn CheckAndReturn(
        Timed: TimedMessage,
        config: crate::Config,
    ) -> stdResult<String, Box<dyn Error>> {
        let I_Timestamp: i64 = Timed.Timestamp.parse()?;
        //TODO:
        if ((I_Timestamp - Utc::now().naive_utc().timestamp_micros()).abs()
            < (config.sTimeTolerance.unwrap_or(30u64) * 1000 * 1000)
                .try_into()
                .unwrap())
        {
            return Ok(Timed.Message);
        } else {
            Err(Box::new(MessageTooOldOrTooNewError))
        }
    }
}

pub mod AlignedMessage {
    #[derive(Debug)]
    pub struct AlignedMessage {
        pub size: usize,
        pub data: Vec<u8>,
    }
}

pub mod Pinning {
    pub fn Try(maybe_cpus: Option<[usize; 4]>, index: usize) {
        if let Some(cpus) = maybe_cpus {
            let _ = core_affinity::set_for_current(core_affinity::CoreId {
                id: cpus[index],
            });
        }
    }
}

pub enum OrderedReliableStream {
    Tcp(TcpStream),
    Uds(UnixStream),
}
pub trait ClonableSendableReceivable {
    fn try_clone(&self) -> Result<Self, std::io::Error>
    where
        Self: Sized;
    fn read(&mut self, buf: &mut [u8]) -> Result<usize, std::io::Error>;
    fn flush(&mut self) -> Result<(), std::io::Error>;
    fn write(&mut self, buf: &[u8]) -> Result<usize, std::io::Error>;
}
impl ClonableSendableReceivable for OrderedReliableStream {
    fn try_clone(&self) -> Result<Self, std::io::Error> {
        match self {
            OrderedReliableStream::Tcp(t) => {
                Ok(OrderedReliableStream::Tcp(t.try_clone()?))
            }
            OrderedReliableStream::Uds(u) => {
                Ok(OrderedReliableStream::Uds(u.try_clone()?))
            }
        }
    }
    fn read(&mut self, buf: &mut [u8]) -> Result<usize, std::io::Error> {
        match self {
            OrderedReliableStream::Tcp(t) => t.read(buf),
            OrderedReliableStream::Uds(u) => u.read(buf),
        }
    }
    fn write(&mut self, buf: &[u8]) -> Result<usize, std::io::Error> {
        match self {
            OrderedReliableStream::Tcp(t) => t.write(buf),
            OrderedReliableStream::Uds(u) => u.write(buf),
        }
    }
    fn flush(&mut self) -> Result<(), std::io::Error> {
        match self {
            OrderedReliableStream::Tcp(ref mut t) => Ok(t.flush()?),
            OrderedReliableStream::Uds(ref mut u) => Ok(u.flush()?),
        }
    }
}
