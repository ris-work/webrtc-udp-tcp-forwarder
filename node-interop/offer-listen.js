import {conf} from "./conf.mjs"
import {timedMessage} from "./timedMessage.mjs"
import {hashAuthenticatedMessage} from "./hashAuthenticatedMessage.mjs"
import {WebSocket} from 'ws'
import wrtc from 'wrtc'
import * as dgram from 'dgram'
import * as net from 'net'

console.assert(conf.PublishType == "wss");

const selftest=true
if(selftest){
let am = new hashAuthenticatedMessage('hello', 'hello');
am.compute().then(console.log);
am.compute().then(verify);
console.log(am);
function verify(result){
hashAuthenticatedMessage.verifyAndReturn('hello', 'hello', result);
}
}
let EndpointURL = conf.PublishEndpoint.split('//').slice(1).join('//')
let wsurl = `wss://${conf.PublishAuthUser}:${conf.PublishAuthPass}@${EndpointURL}`
console.log(wsurl)
let sigSocket = new WebSocket(wsurl);
sigSocket.addEventListener('message', (e) => console.log(e.data))
let otherSocket;
if(net.isIPv6(conf.Address)){
otherSocket = dgram.createSocket('udp6');
}
else if (net.isIPv4(conf.Address)) {
otherSocket = dgram.createSocket('udp4');
}
else{console.log("neither IPv4 nor 6")}
let addrPortPair = `${conf.Address}:${conf.Port}`
console.log(`Should listen on: ${addrPortPair}`)
otherSocket.on('message', (msg, rinfo) => console.log(rinfo))
otherSocket.on('listening', () => console.log(`Listening on: ${JSON.stringify(otherSocket.address())}`))
otherSocket.bind(conf.Port, conf.Address)

let transformedICEServers = [];
for (const serverList in conf.ICEServers) {
	let transformedServerList = {};
	for(const key in conf.ICEServers[serverList]){
		Object.defineProperty(transformedServerList, key.toLowerCase(), {value: conf.ICEServers[serverList][key], enumerable: true});
	}
	transformedICEServers.push(transformedServerList);
}
const RTCConfig = {iceServers: transformedICEServers}
console.log(JSON.stringify(RTCConfig))

let pc_state_change = (x) => console.dir(x)
let pc_ice_error = (x) => console.dir(x)
let pc_ice_gathering_change = (x) => console.dir(x)
let pc_ice_candidate = (x) => {console.dir(x); if(x.candidate==null) console.log(pc.localDescription)}
let pc_negotiation_needed = (x) => pc.createOffer().then(offerReady)

let offerReady = (x) => {console.dir(x); pc.setLocalDescription(x);}

let dc_open = (x) => console.dir(x)
let dc_close = (x) => console.dir(x)
let incoming_dc_message = (e) => console.dir(e)

let pc = new wrtc.RTCPeerConnection(RTCConfig);
pc.addEventListener('connectionstatechange', pc_state_change);
pc.addEventListener('icegatheringerror', pc_ice_error);
pc.addEventListener('icegatheringstatechange', pc_ice_gathering_change);
pc.addEventListener('icecandidate', pc_ice_candidate);

let dc = pc.createDataChannel('data');
dc.addEventListener('message', incoming_dc_message);
dc.addEventListener('open', dc_open);
dc.addEventListener('close', dc_close);


pc.addEventListener('negotiationneeded', pc_negotiation_needed);
setTimeout(() => console.log("Gathered so far: " + JSON.stringify(pc.localDescription)), 5000)
