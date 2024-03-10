export class timedMessage{
	constructor(message){
		this.Message=Message;
		this.Timestamp=parseInt((new Date()).getTime()+'000')
	}
	static checkAndReturn(timed){
		let now=parseInt((new Date()).getTime()+'000')
		if (Math.abs(timed.Timestamp - now) > 15*1000*1000) 
			throw new Error("Message too old or too new.");
		else {
			return timed.message;
		}
	}
}
