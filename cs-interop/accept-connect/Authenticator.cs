using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Rishi.Kexd;
public class AuthenticatedMessage
{
	[JsonInclude] private string TimedMessage;
	[JsonInclude] private string MAC;
	public byte[] Hash;
	AuthenticatedMessage(string Message, byte[] AC)
	{
		this.TimedMessage = Message;
		using (HMACSHA256 hmac = new HMACSHA256(AC))
		{
			byte[] messageBytes = Encoding.UTF8.GetBytes(TimedMessage);
			byte[] hash = hmac.ComputeHash(messageBytes);
			this.Hash = hash;
			this.MAC = Convert.ToHexString(hash);
		}
	}
	public string GetMessage(byte[] AC)
	{
		using (HMACSHA256 hmac = new HMACSHA256(AC))
		{
			byte[] messageBytes = Encoding.UTF8.GetBytes(this.TimedMessage);
			byte[] hash = hmac.ComputeHash(messageBytes);
			if (String.Compare(this.MAC, Convert.ToHexString(hash), StringComparison.OrdinalIgnoreCase) == 0)
			{
				return this.TimedMessage;
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
	[JsonInclude] private string Message;
	[JsonInclude] private string Timestamp;
	TimedMessage(string Message)
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
