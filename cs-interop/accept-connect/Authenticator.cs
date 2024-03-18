using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Rishi.Kexd;
public class AuthenticatedMessage{
	[JsonInclude] private string message;
	[JsonInclude] private string MAC;
	public byte[] Hash;
	AuthenticatedMessage(string Message, byte[] AC){
		this.message=Message;
		using (HMACSHA256 hmac = new HMACSHA256(AC)){
			byte[] messageBytes = Encoding.UTF8.GetBytes(Message);
			byte[] hash = hmac.ComputeHash(messageBytes);
			this.Hash = hash;
		}
	}
	public string GetMessage(byte[] AC){
		using (HMACSHA256 hmac = new HMACSHA256(AC)){
			byte[] messageBytes = Encoding.UTF8.GetBytes(this.message);
			byte[] hash = hmac.ComputeHash(messageBytes);
			if (this.Hash == hash) {
				return this.message;
			}
			else {
				throw new MacVerificationFailed();
			}
		}
	}
}

public class TimedMessage{
	[JsonInclude] private string message;
	[JsonInclude] private int TAITimestamp;
	TimedMessage(string Message){
		this.message = Message; 
		this.TAITimestamp = (int)(DateTime.UtcNow-DateTime.UnixEpoch).TotalMicroseconds;
	}
	public string GetMessage(int ToleranceMicroS = 600*1000*1000){
		int currentTAITimestamp = (int)(DateTime.UtcNow-DateTime.UnixEpoch).TotalMicroseconds;
		if (Math.Abs(currentTAITimestamp-this.TAITimestamp)<ToleranceMicroS){
			return this.message;
		}
		else {
			throw new TimeVerificationFailed();
		}
	}
}

[Serializable]
public class MacVerificationFailed: Exception {
	public MacVerificationFailed(): base(){}
	public MacVerificationFailed(string message): base(message){}
	public MacVerificationFailed(string message, Exception inner): base(message, inner){}
}
[Serializable]
public class TimeVerificationFailed: Exception {
	public TimeVerificationFailed(): base(){}
	public TimeVerificationFailed(string message): base(message){}
	public TimeVerificationFailed(string message, Exception inner): base(message, inner){}
}
