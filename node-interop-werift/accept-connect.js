import { conf } from "./conf.mjs";
import { timedMessage } from "./timedMessage.mjs";
import { hashAuthenticatedMessage } from "./hashAuthenticatedMessage.mjs";
import { WebSocket } from "ws";
import wrtc from "wrtc";
import * as process from "process";
import * as dgram from "dgram";
import * as net from "net";
import * as b64 from "nodejs-base64";

console.assert(conf.WebRTCMode == "Accept");
console.assert(conf.PublishType == "ws");

let offerUnvalidated;
let connected = false;

let to_dc = (x) => {
	to_dc_queue.push(x);
};
let to_os = (x) => {
	to_os_queue.push(x);
};
let to_dc_queue = [];
let to_os_queue = [];

let MAX_BUF = 1024 * 1024;

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
sigSocket.addEventListener("close", (e) => {
	console.warn("Websocket: closed");
	if (!connected) process.exit(1);
});
sigSocket.addEventListener("open", (e) => {
	proceedToWebRTC();
});
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
otherSocket.on("message", (msg, rinfo) => {
	if (selftest) console.log(rinfo);
	to_dc(msg.buffer);
});
otherSocket.on("listening", () =>
	console.log(`Listening on: ${JSON.stringify(otherSocket.address())}`)
);
otherSocket.on("connect", () => {
	console.log(`to_os_queue: ${to_os_queue.length}`);
	console.log("oS connected.");
	to_os = (x) => otherSocket.send(x);
	/* flush */
	to_os_queue.forEach((v) => to_os(v));
});
otherSocket.connect(conf.Port, conf.Address);

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

let pc_state_change = (x) => {
	console.log(
		"Peer conenction state: " + JSON.stringify(x) + " " + pc.connectionState
	);
	if (pc.connectionState == "connected") connected = true;
};
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

let pc_on_dc = function (e) {
	console.log("DataChannel event");
	dc = e.channel;
	//dc.binaryType = "blob";
	dc.binaryType = "arraybuffer";
	dc.addEventListener("open", dc_open);
	dc.addEventListener("close", dc_close);
	dc.addEventListener("message", dc_inc);
};
let dc_open = () => {
	console.log("DC open");
	to_dc = (x) => {
		if (dc.bufferedAmount < MAX_BUF) dc.send(x);
	};
	/* flush */
	to_dc_queue.forEach((v) => to_dc(v));
};
let dc_close = () => {
	console.log("DC closed");
	setTimeout(process.exit(0), 2000);
};
let dc_inc = (e) => {
	if (selftest)
		console.log(
			`DC incoming: ${e.data} ${
				e.data.byteLength
			} ${typeof e.data} ${JSON.stringify(e)}`
		);
	to_os(Buffer.from(e.data));
};

let pc, dc;
let gotOffer;

function proceedToWebRTC() {
	gotOffer = async function () {
		//let timeValidated = timedMessage.checkAndReturn(JSON.parse(offerUnvalidated));
		let oU = JSON.parse(offerUnvalidated);
		console.log(oU);
		let hashValidated = await hashAuthenticatedMessage.verifyAndReturn(
			oU.MessageWithTime,
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
		pc.addEventListener("datachannel", pc_on_dc);

		/*dc = pc.createDataChannel('data');
dc.addEventListener('message', incoming_dc_message);
dc.addEventListener('open', dc_open);
dc.addEventListener('close', dc_close);
*/

		pc.setRemoteDescription(
			new wrtc.RTCSessionDescription(
				JSON.parse(b64.base64decode(timeValidated))
			)
		);
		//pc.addEventListener('negotiationneeded', pc_negotiation_needed);
		//pc.setRemoteDescripotion
		let answer = await pc.createAnswer();
		pc.setLocalDescription(answer);
		if (selftest)
			setTimeout(
				() =>
					console.log(
						"Gathered so far: " + JSON.stringify(pc.localDescription)
					),
				8000
			);
		setTimeout(
			() => doneGeneratingAnswer(JSON.stringify(pc.localDescription)),
			2500
		);
	};

	function doneGeneratingAnswer(answer) {
		let timed = new timedMessage(b64.base64encode(answer));
		let serializedTimed = JSON.stringify(timed);
		let hmacMessage = new hashAuthenticatedMessage(
			serializedTimed,
			conf.PeerPSK
		);
		hmacMessage.compute().then(() => sendAnswer(hmacMessage));
	}
	function sendAnswer(hmacMessage) {
		console.log(hmacMessage);
		sigSocket.send(JSON.stringify(hmacMessage));
	}
}
