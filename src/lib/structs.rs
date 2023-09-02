mod structs{
#[derive(Deserialize)]
    struct Config {
        Type: String,
        WebRTCMode: String,
        Address: Option<String>,
        Port: Option<String>,
        ICEServers: Vec<ICEServer>,
        Ordered: Option<bool>,
        ConHost: Option<bool>,
        Publish: Option<bool>,
        PublishType: Option<String>,
        PublishEndpoint: Option<String>,
        PublishAuthType: Option<String>,
        PublishAuthUser: Option<String>,
        PublishAuthPass: Option<String>,
        PublishPSK: Option<String>
    }
}
