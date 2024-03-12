import { conf } from "./conf.mjs";
import { timedMessage } from "./timedMessage.mjs";
import { hashAuthenticatedMessage } from "./hashAuthenticatedMessage.mjs";
import { WebSocket } from "ws";
import wrtc from "wrtc";
import * as dgram from "dgram";
import * as net from "net";

console.assert(conf.WebRTCMode == "Accept");
console.assert(conf.PublishType == "ws");

let offerUnvalidated;

const selftest = true;
if (selftest) {
	let am = new hashAuthenticatedMessage("hello", "hello");
	am.compute().then(console.log);
	am.compute().then(verify);
	console.log(am);
	function verify(result) {
		hashAuthenticatedMessage.verifyAndReturn("hello", "hello", result);
	}
}
let EndpointURL = conf.PublishEndpoint.split("//").slice(1).join("//");
let wsurl = `wss://${conf.PublishAuthUser}:${conf.PublishAuthPass}@${EndpointURL}`;
console.log(wsurl);
let sigSocket = new WebSocket(wsurl);
sigSocket.addEventListener("message", (e) => {
	console.log(e.data);
	offerUnvalidated = e.data;
	gotOffer();
});
let otherSocket;
if (net.isIPv6(conf.Address)) {
	otherSocket = dgram.createSocket("udp6");
} else if (net.isIPv4(conf.Address)) {
	otherSocket = dgram.createSocket("udp4");
} else {
	console.log("neither IPv4 nor 6");
}
let addrPortPair = `${conf.Address}:${conf.Port}`;
console.log(`Should listen on: ${addrPortPair}`);
otherSocket.on("message", (msg, rinfo) => console.log(rinfo));
otherSocket.on("listening", () =>
	console.log(`Listening on: ${JSON.stringify(otherSocket.address())}`)
);
otherSocket.bind(conf.Port, conf.Address);

let transformedICEServers = [];
for (const serverList in conf.ICEServers) {
	let transformedServerList = {};
	for (const key in conf.ICEServers[serverList]) {
		Object.defineProperty(transformedServerList, key.toLowerCase(), {
			value: conf.ICEServers[serverList][key],
			enumerable: true,
		});
	}
	transformedICEServers.push(transformedServerList);
}
const RTCConfig = { iceServers: transformedICEServers };
if (selftest) console.log(JSON.stringify(RTCConfig));

let pc_state_change = (x) => console.dir(x);
let pc_ice_error = (x) => console.dir(x);
let pc_ice_gathering_change = (x) => console.dir(x);
let pc_ice_candidate = (x) => {
	if (selftest) console.dir(x);
	if (x.candidate == null) console.log(pc.localDescription);
};
//let pc_negotiation_needed = (x) => pc.createAnswer().then((ans) => {console.log(ans); pc.setLocalDescription(ans).then(console.log)})

let answerReady = (x) => {
	console.dir(x);
};

let dc_open = (x) => console.dir(x);
let dc_close = (x) => console.dir(x);
let incoming_dc_message = (e) => console.dir(e);

let pc, dc;

async function gotOffer() {
	//let timeValidated = timedMessage.checkAndReturn(JSON.parse(offerUnvalidated));
	let oU = JSON.parse(offerUnvalidated);
	console.log(oU);
	let hashValidated = await hashAuthenticatedMessage.verifyAndReturn(
		oU.Message,
		conf.PeerPSK,
		oU.MAC
	);
	console.log({ hV: hashValidated });
	let timeValidated = timedMessage.checkAndReturn(JSON.parse(hashValidated));
	console.log({ tV: timeValidated });
	pc = new wrtc.RTCPeerConnection(RTCConfig);
	pc.addEventListener("connectionstatechange", pc_state_change);
	pc.addEventListener("icegatheringerror", pc_ice_error);
	pc.addEventListener("icegatheringstatechange", pc_ice_gathering_change);
	pc.addEventListener("icecandidate", pc_ice_candidate);

	/*dc = pc.createDataChannel('data');
dc.addEventListener('message', incoming_dc_message);
dc.addEventListener('open', dc_open);
dc.addEventListener('close', dc_close);
*/

	pc.setRemoteDescription(
		new wrtc.RTCSessionDescription(JSON.parse(timeValidated))
	);
	//pc.addEventListener('negotiationneeded', pc_negotiation_needed);
	//pc.setRemoteDescripotion
	let answer = await pc.createAnswer();
	pc.setLocalDescription(answer);
	if (selftest)
		setTimeout(
			() =>
				console.log("Gathered so far: " + JSON.stringify(pc.localDescription)),
			8000
		);
	//setTimeout(() => doneGeneratingOffer(JSON.stringify(pc.localDescription)), 5000)
}

function doneGeneratingOffer(offer) {
	let timed = new timedMessage(offer);
	let serializedTimed = JSON.stringify(timed);
	let hmacMessage = new hashAuthenticatedMessage(serializedTimed, conf.PeerPSK);
	hmacMessage.compute().then(() => sendOffer(hmacMessage));
}
function sendOffer(hmacMessage) {
	console.log(hmacMessage);
	sigSocket.send(JSON.stringify(hmacMessage));
}
