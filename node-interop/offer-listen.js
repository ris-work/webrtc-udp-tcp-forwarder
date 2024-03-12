import { conf } from "./conf.mjs";
import { timedMessage } from "./timedMessage.mjs";
import { hashAuthenticatedMessage } from "./hashAuthenticatedMessage.mjs";
import { WebSocket } from "ws";
import wrtc from "wrtc";
import * as dgram from "dgram";
import * as process from "process";
import * as net from "net";

console.assert(conf.WebRTCMode == "Offer");
console.assert(conf.PublishType == "ws");

let connected = false;
let answerUnvalidated;

const selftest = false;
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
	answerUnvalidated = e.data;
	gotAnswer();
});
sigSocket.addEventListener("close", (e) => {
	if (!connected) process.exit(1);
});
sigSocket.addEventListener("open", (e) => {
	proceedToWebRTC();
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
let pc_negotiation_needed = (x) => pc.createOffer().then(offerReady);

let offerReady = (x) => {
	console.dir(x);
	pc.setLocalDescription(x);
};

let dc_open = (x) => console.dir(x);
let dc_close = (x) => console.dir(x);
let incoming_dc_message = (e) => console.dir(e);

let pc = new wrtc.RTCPeerConnection(RTCConfig);
let gotAnswer;
function proceedToWebRTC() {
	pc.addEventListener("connectionstatechange", pc_state_change);
	pc.addEventListener("icegatheringerror", pc_ice_error);
	pc.addEventListener("icegatheringstatechange", pc_ice_gathering_change);
	pc.addEventListener("icecandidate", pc_ice_candidate);

	let dc = pc.createDataChannel("data");
	dc.addEventListener("message", incoming_dc_message);
	dc.addEventListener("open", dc_open);
	dc.addEventListener("close", dc_close);

	pc.addEventListener("negotiationneeded", pc_negotiation_needed);
	if (selftest)
		setTimeout(
			() =>
				console.log("Gathered so far: " + JSON.stringify(pc.localDescription)),
			5000
		);
	setTimeout(
		() => doneGeneratingOffer(JSON.stringify(pc.localDescription)),
		2500
	);

	function doneGeneratingOffer(offer) {
		let timed = new timedMessage(offer);
		let serializedTimed = JSON.stringify(timed);
		let hmacMessage = new hashAuthenticatedMessage(
			serializedTimed,
			conf.PeerPSK
		);
		hmacMessage.compute().then(() => sendOffer(hmacMessage));
	}
	function sendOffer(hmacMessage) {
		console.log(hmacMessage);
		sigSocket.send(JSON.stringify(hmacMessage));
	}
	gotAnswer = async function () {
		//let timeValidated = timedMessage.checkAndReturn(JSON.parse(offerUnvalidated));
		let aU = JSON.parse(answerUnvalidated);
		console.log(aU);
		let hashValidated = await hashAuthenticatedMessage.verifyAndReturn(
			aU.Message,
			conf.PeerPSK,
			aU.MAC
		);
		console.log({ hV: hashValidated });
		let timeValidated = timedMessage.checkAndReturn(JSON.parse(hashValidated));
		console.log({ tV: timeValidated });
		pc.setRemoteDescription(
			new wrtc.RTCSessionDescription(JSON.parse(timeValidated))
		);
	};
}
