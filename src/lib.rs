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
