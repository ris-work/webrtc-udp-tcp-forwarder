use chrono::naive::NaiveDateTime;
use chrono::Utc;
use serde::Deserialize;
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
    ) -> stdResult<String, Box<dyn Error>> {
        let MAC = hex::decode(Msg.MAC)?;
        let mut MacGen = Hmac::<Sha256>::new_from_slice(
            config.PeerPSK.expect("No peer PSK provided.").as_bytes(),
        )?;
        MacGen.update(Msg.MessageWithTime.as_bytes());
        MacGen.verify_slice(&MAC[..])?;
        Ok(Msg.MessageWithTime)
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
    pub fn ConstructMessage(Message: String) -> TimedMessage {
        return TimedMessage {
            Timestamp: Utc::now().naive_utc().timestamp_micros().to_string(),
            Message: Message,
        };
    }
    pub fn CheckAndReturn(Timed: TimedMessage) -> stdResult<String, Box<dyn Error>> {
        let I_Timestamp: i64 = Timed.Timestamp.parse()?;
        //TODO:
        if (I_Timestamp > Utc::now().naive_utc().timestamp_micros() - 15 * 1000 * 1000) {
            return Ok(Timed.Message);
        } else {
            Err(Box::new(MessageTooOldOrTooNewError))
        }
    }
}
