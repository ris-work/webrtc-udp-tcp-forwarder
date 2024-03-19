using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Rishi.Kexd;
public class AuthenticatedMessage
{
	[JsonInclude][JsonRequired] private string MessageWithTime;
	[JsonInclude][JsonRequired] private string MAC;
	public byte[] Hash;
	public AuthenticatedMessage() { }
	public AuthenticatedMessage(string Message, byte[] AC)
	{
		this.MessageWithTime = Message;
		using (HMACSHA256 hmac = new HMACSHA256(AC))
		{
			byte[] messageBytes = Encoding.UTF8.GetBytes(MessageWithTime);
			byte[] hash = hmac.ComputeHash(messageBytes);
			this.Hash = hash;
			this.MAC = Convert.ToHexString(hash);
		}
	}
	public string GetMessage(byte[] AC)
	{
		using (HMACSHA256 hmac = new HMACSHA256(AC))
		{
			byte[] messageBytes = Encoding.UTF8.GetBytes(this.MessageWithTime);
			byte[] hash = hmac.ComputeHash(messageBytes);
			if (String.Compare(this.MAC, Convert.ToHexString(hash), StringComparison.OrdinalIgnoreCase) == 0)
			{
				return this.MessageWithTime;
			}
			else
			{
				throw new MacVerificationFailed();
			}
		}
	}
}

public class TimedMessage
{
	[JsonInclude][JsonRequired] private string Message;
	[JsonInclude][JsonRequired] private string Timestamp;
	public TimedMessage() { }
	/*[JsonConstructor] public TimedMessage(string Message)
	{
		this.Message = Message;
		this.Timestamp = ((Int64)(DateTime.UtcNow - DateTime.UnixEpoch).TotalMicroseconds).ToString();
	}*/
	public TimedMessage(string Message)
	{
		this.Message = Message;
		this.Timestamp = ((Int64)(DateTime.UtcNow - DateTime.UnixEpoch).TotalMicroseconds).ToString();
	}
	public string GetMessage(int ToleranceMicroS = 600 * 1000 * 1000)
	{
		long currentTAITimestamp = (Int64)(DateTime.UtcNow - DateTime.UnixEpoch).TotalMicroseconds;
		if (Math.Abs(currentTAITimestamp - Int64.Parse(this.Timestamp)) < ToleranceMicroS)
		{
			return this.Message;
		}
		else
		{
			throw new TimeVerificationFailed();
		}
	}
}

[Serializable]
public class MacVerificationFailed : Exception
{
	public MacVerificationFailed() : base() { }
	public MacVerificationFailed(string message) : base(message) { }
	public MacVerificationFailed(string message, Exception inner) : base(message, inner) { }
}
[Serializable]
public class TimeVerificationFailed : Exception
{
	public TimeVerificationFailed() : base() { }
	public TimeVerificationFailed(string message) : base(message) { }
	public TimeVerificationFailed(string message, Exception inner) : base(message, inner) { }
}
