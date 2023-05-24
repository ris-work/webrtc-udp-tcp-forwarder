/* eslint-env browser */

// SPDX-FileCopyrightText: 2023 The Pion community <https://pion.ly>
// SPDX-License-Identifier: MIT

const pc = new RTCPeerConnection({
	iceServers: [
		{
			urls: 'stun:stun.l.google.com:19302'
		}
	]
})
const log = msg => {
	document.getElementById('logs').innerHTML += msg + '<br>'
}


pc.oniceconnectionstatechange = e => log(pc.iceConnectionState)
pc.onicecandidate = event => {
	if (event.candidate === null) {
		document.getElementById('localSessionDescription').value = btoa(JSON.stringify(pc.localDescription))
	}
}

pc.onnegotiationneeded = e =>
	pc.createOffer().then(d => pc.setLocalDescription(d)).catch(log)

window.sendMessage = () => {
	const message = document.getElementById('message').value
	if (message === '') {
		return alert('Message must not be empty')
	}

	dc.send(message)
}

window.startSession = () => {
	const sd = document.getElementById('remoteSessionDescription').value
	dc = pc.createDataChannel('data')
	dc.onclose = () => console.log('sendChannel has closed')
	dc.onopen = () => console.log('sendChannel has opened')
	dc.onmessage = e => {
		log(`Message: '${dc.label}' receives '${(new TextDecoder()).decode(new Uint8Array(e.data))}'`)
		console.log(`Message: '${dc.label}' receives '${e.data}'`)
	}
	if (sd === '') {
		return alert('Session Description must not be empty')
	}
	try {
		pc.setRemoteDescription(JSON.parse(atob(sd)))
		pc.createAnswer().then((x)=>pc.setLocalDescription(x));
	} catch (e) {
		alert(e)
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
