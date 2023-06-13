/* eslint-env browser */

// SPDX-FileCopyrightText: 2023 The Pion community <https://pion.ly>
// SPDX-License-Identifier: MIT

const pc = new RTCPeerConnection({
	iceServers: [
		{
			urls: ['stun:stun.l.google.com:19302', 'stun:vz.al']
		}
	]
})
const log = msg => {
	document.getElementById('logs').innerHTML += "<span class='time'>["+(new Date(Date.now())).toISOString()+"]</span> " + msg + '<br>'
}


pc.oniceconnectionstatechange = e => log(pc.iceConnectionState)
pc.onicecandidate = event => {
	console.log(event);
	if (event.candidate === null) {
		document.getElementById('localSessionDescription').value = btoa(JSON.stringify(pc.localDescription))
		console.log(JSON.stringify(pc.localDescription));
	}
}

pc.onnegotiationneeded = e =>
	pc.createOffer().then(d => pc.setLocalDescription(d)).catch(log)



window.startSession = () => {
	const sd = document.getElementById('remoteSessionDescription').value
	const decoded = document.getElementById('decoded')
	//dc = pc.createDataChannel('data')
	pc.ondatachannel = function(e){
		log("Ondatachannel event.")
		let dc = e.channel;
		dc.onclose = () => console.log('sendChannel has closed')
		dc.onopen = () => console.log('sendChannel has opened')
		dc.onmessage = async function(e){let x;
			try{
				if(e.data.text) {x = (await e.data.text())}
				else {x = (new TextDecoder).decode(new Uint8Array(e.data)); }
			}
			catch (err) {console.log(err)};
			document.getElementById("messages_cons").innerHTML += x;
			log(`Message: '${dc.label}' receives '${x}'`)
		}
		window.sendMessage = () => {
			const message = document.getElementById('message').value
			if (message === '') {
				return alert('Message must not be empty')
			}

			dc.send(message)
		}
	}
	if (sd === '') {
		return alert('Session Description must not be empty')
	}
	try {
		console.log((new RTCSessionDescription (JSON.parse(atob(sd)))));
		decoded.innerHTML=JSON.stringify(JSON.parse(atob(sd)), null, "\t").replaceAll("\\r\\n", "\n\t\t");
		pc.setRemoteDescription(new RTCSessionDescription (JSON.parse(atob(sd)))).then(log).catch(log);
		pc.createAnswer().then((x)=>{pc.setLocalDescription(x); console.log(x)});
	} catch (e) {
		alert(e)
		log(e)
	}
}

window.copySDP = () => {
	const browserSDP = document.getElementById('localSessionDescription')

	browserSDP.focus()
	browserSDP.select()

	try {
		const successful = document.execCommand('copy')
		const msg = successful ? 'successful' : 'unsuccessful'
		log('Copying SDP was ' + msg)
	} catch (err) {
		log('Unable to copy SDP ' + err)
	}
}
