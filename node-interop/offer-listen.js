import {conf} from "./conf.mjs"
import {timedMessage} from "./timedMessage.mjs"
import {hashAuthenticatedMessage} from "./hashAuthenticatedMessage.mjs"

let am = new hashAuthenticatedMessage('hello', 'hello');
am.compute().then(console.log);
am.compute().then(verify);
console.log(am);
function verify(result){
hashAuthenticatedMessage.verifyAndReturn('hello', 'hello', result);
}
