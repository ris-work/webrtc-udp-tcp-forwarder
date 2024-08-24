import { conf } from "./conf.mjs";
import { timedMessage } from "./timedMessage.mjs";
import { hashAuthenticatedMessage } from "./hashAuthenticatedMessage.mjs";
import { WebSocket } from "ws";
import wrtc from "wrtc";
import * as dgram from "dgram";
import * as process from "process";
import * as net from "net";
import * as b64 from "nodejs-base64";

console.assert(conf.WebRTCMode == "Offer");
console.assert(conf.PublishType == "ws");

let connected = false;
let answerUnvalidated;

let to_dc = (x) => {
	to_dc_queue.push(x);
};
let to_os = (x) => {
	to_os_queue.push(x);
};
let to_dc_queue = [];
let to_os_queue = [];

let os_connected = false;

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
sigSocket.addEventListener("message", (e) => {
	console.log(e.data);
	answerUnvalidated = e.data;
	gotAnswer();
});
sigSocket.addEventListener("close", (e) => {
	console.warn("websocket: closed");
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
otherSocket.on("message", (msg, rinfo) => {
	if (selftest) {
		console.log(rinfo);
		console.log(JSON.stringify(msg.buffer));
		console.log(typeof msg.buffer);
	}
	if (!os_connected) {
		os_connected = true;
		otherSocket.connect(rinfo.port, rinfo.address);
	}
	to_dc(msg.buffer);
});
otherSocket.on("connect", () => {
	console.log(`to_os_queue: ${to_os_queue.length}`);
	console.log("oS connected.");
	to_os = (x) => {
		TSLOSS = 0;
		otherSocket.send(x);
	};
	/* flush */
	to_os_queue.forEach((v) => to_os(v));
});
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

let pc_state_change = (x) => {
	console.log(
		"Peer connection state: " + JSON.stringify(x) + " " + pc.connectionState
	);
	if (pc.connectionState == "connected") {
		connected = true;
	}
};
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

let dc_open = () => {
	console.log("DC open");
	console.log(`to_dc_queue: ${to_dc_queue.length}`);
	to_dc = (x) => {
		TSLDCS = 0;
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
		console.log(`DC incoming: ${JSON.stringify(e.data.byteLength)}`);
	to_os(Buffer.from(e.data));
};

let pc = new wrtc.RTCPeerConnection(RTCConfig);
let dc;
let gotAnswer;
function proceedToWebRTC() {
	pc.addEventListener("connectionstatechange", pc_state_change);
	pc.addEventListener("icegatheringerror", pc_ice_error);
	pc.addEventListener("icegatheringstatechange", pc_ice_gathering_change);
	pc.addEventListener("icecandidate", pc_ice_candidate);

	dc = pc.createDataChannel("data", {
		ordered: false,
		// maxPacketLifetime: 0,
		maxRetransmts: 0,
	});
	dc.addEventListener("message", dc_inc);
	dc.addEventListener("open", dc_open);
	dc.addEventListener("close", dc_close);
	//dc.binaryType = "blob";
	dc.binaryType = "arraybuffer";

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
		let timed = new timedMessage(b64.base64encode(offer));
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
			aU.MessageWithTime,
			conf.PeerPSK,
			aU.MAC
		);
		console.log({ hV: hashValidated });
		let timeValidated = timedMessage.checkAndReturn(JSON.parse(hashValidated));
		console.log({ tV: timeValidated });
		pc.setRemoteDescription(
			new wrtc.RTCSessionDescription(
				JSON.parse(b64.base64decode(timeValidated))
			)
		);
	};
}

let TSLDCS = 0; // Time since last DC send
let TSLOSS = 0;

setInterval(function () {
	TSLDCS++;
	TSLOSS++;
	if (TSLDCS >= conf.TimeoutCountMax || TSLOSS >= conf.TimeoutCountMax) {
		console.log("Exiting due to inactivity...");
		process.exit(0);
	}
}, 1000);
