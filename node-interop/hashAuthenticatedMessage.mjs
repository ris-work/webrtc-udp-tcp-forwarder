import hmac from "js-crypto-hmac";
const hashAlgo = "SHA-256";
export class hashAuthenticatedMessage {
	#key8;
	MAC;
	MAC8;
	Message;
	constructor(message, key) {
		this.Message = message;
		this.Message8 = new Uint8Array(message.length);
		let encoder = new TextEncoder();
		encoder.encodeInto(message, this.Message8);
		this.#key8 = new Uint8Array(key.length);
		encoder.encodeInto(key, this.#key8);
	}
	async compute() {
		this.MAC8 = await hmac.compute(this.#key8, this.Message, hashAlgo);
		this.MAC = "";
		for (let i = 0; i < this.MAC8.length; i++) {
			this.MAC += this.MAC8[i].toString(16).padStart(2, 0);
		}
		console.log(this.MAC8);
		return this.MAC;
	}
	static async verifyAndReturn(message, key, hash) {
		let encoder = new TextEncoder();
		let key8 = new Uint8Array(key.length);
		let message8 = new Uint8Array(message.length);
		let hashA = hash.split("");
		let hash8tmp = [];
		let b = "";
		console.log(hashA);
		while (hashA.length !== 0) {
			let b = hashA.splice(0, 2);
			hash8tmp.push(parseInt(b.join(""), 16));
		}
		message8 = new Uint8Array(message.length);
		encoder.encodeInto(message, message8);
		let hash8 = new Uint8Array(hash8tmp);
		encoder.encodeInto(key, key8);
		let result = await hmac.verify(key8, message8, hash8, hashAlgo);
		console.log(key8);
		console.log(message8);
		console.log(hash8);
		if (result === false)
			throw new Error(`MAC Error, expected: ${hash}, got something else`);
		else return message;
	}
}
