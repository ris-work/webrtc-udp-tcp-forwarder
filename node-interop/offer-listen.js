import {conf} from "./conf.mjs"
import {timedMessage} from "./timedMessage.mjs"
import {hashAuthenticatedMessage} from "./hashAuthenticatedMessage.mjs"
import {WebSocket} from 'ws'
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
