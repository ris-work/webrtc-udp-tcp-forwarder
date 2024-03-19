//-----------------------------------------------------------------------------
// Filename: Program.cs
//
// Description: An example WebRTC server application that attempts to establish
// a WebRTC data channel with a remote peer.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
// 
// History:
// 12 Sep 2020	Aaron Clauson	Created, Dublin, Ireland.
// 09 Apr 2021  Aaron Clauson   Updated for new SCTP stack and added crude load
//                              test capability.
// 19 Mar 2024	Rishikeshan	Changed it.
//
// License: 
// OSLv3 ONLY (no later)
// Previous Ancestor License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Serilog.Extensions.Logging;
using System.IO;
using SIPSorcery.Net;
using SIPSorcery.Sys;
using WebSocketSharp.Server;
using Tomlyn;
using Tomlyn.Model;
using Rishi.Kexd;
using System.Diagnostics.CodeAnalysis;

namespace demo
{
	class Program
	{
		private static TomlTable? confModel = null;
		private const int WEBSOCKET_PORT = 8081;
		private const string STUN_URL = "stun:stun.sipsorcery.com";
		private const int JAVASCRIPT_SHA256_MAX_IN_SIZE = 65535;
		private const int SHA256_OUTPUT_SIZE = 32;
		private const int MAX_LOADTEST_COUNT = 100;

		private static Microsoft.Extensions.Logging.ILogger logger = NullLogger.Instance;

		private static uint _loadTestPayloadSize = 0;
		private static int _loadTestCount = 0;

		[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
		[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
		static void Main(string[] args)
		{
			string config = File.ReadAllText(args[0]);
			var model = Toml.ToModel(config);
			confModel = model;

			Console.WriteLine("WebRTC Get Started Data Channel");

			logger = AddConsoleLogger();

			// Start web socket.
			Console.WriteLine("Starting web socket client...");
			var clientSock = new ClientWebSocket();
			//string socketPath = String.Join('/', ((string)model["PublishEndpoint"]).Split('/').Skip(2).ToArray());
			string socketPathRaw = (string)model["PublishEndpoint"];
			string user = (string)model["PublishAuthUser"];
			string password = (string)model["PublishAuthPass"];
			string peerPSK = (string)model["PeerPSK"];
			//string uriString = $"wss://{user}:{password}@{socketPath}";
			string uriString = $"{socketPathRaw}";
			Console.WriteLine(uriString);
			string creds = $"{user}:{password}";
			string credsB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(creds));
			string authString = $"Basic {credsB64}";
			Uri uriWithAuth = new Uri(uriString);
			clientSock.Options.SetRequestHeader("Authorization", authString);
			clientSock.ConnectAsync(uriWithAuth, CancellationToken.None).Wait();
			byte[] offerSignedBytes = new byte[65536];
			//ArraySegment<byte> offerSignedBytesSegment = new ArraySegment<byte>(offerSignedBytes);
			var taskRecv = clientSock.ReceiveAsync(offerSignedBytes, CancellationToken.None);
			taskRecv.Wait();
			var result = taskRecv.Result;
			Console.WriteLine("Got offer string");
			var offerSignedJson = Encoding.UTF8.GetString(offerSignedBytes[0..result.Count]);
			Console.WriteLine(offerSignedJson);
			var jsonOptions = new JsonSerializerOptions()
			{
				IncludeFields = true,
				UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
			};
			AuthenticatedMessage authenticatedOffer = JsonSerializer.Deserialize<AuthenticatedMessage>(offerSignedJson, jsonOptions);
			byte[] peerPSKBytes = Encoding.UTF8.GetBytes(peerPSK);
			string timedOfferJson = authenticatedOffer.GetMessage(peerPSKBytes);
			TimedMessage timedOffer = JsonSerializer.Deserialize<TimedMessage>(timedOfferJson, jsonOptions);
			string offerBase64 = timedOffer.GetMessage();
			byte[] offerBytes = Convert.FromBase64String(offerBase64);
			string offer = Encoding.UTF8.GetString(offerBytes);
			Console.WriteLine(offer);
			var pcTask = CreatePeerConnection(offer);
			pcTask.Wait();
			(var pc, var answer) = pcTask.Result;
			Console.WriteLine(answer);
			byte[] answerBytes = Encoding.UTF8.GetBytes(answer);
			string answerBase64 = Convert.ToBase64String(answerBytes);
			TimedMessage timedAnswer = new TimedMessage(answerBase64);

			string timedAnswerJson = JsonSerializer.Serialize(timedAnswer, jsonOptions);

			AuthenticatedMessage signedAnswer = new AuthenticatedMessage(timedAnswerJson, peerPSKBytes);

			string signedAnswerJson = JsonSerializer.Serialize(signedAnswer, jsonOptions);
			byte[] signedAnswerBytes = Encoding.UTF8.GetBytes(signedAnswerJson);
			clientSock.SendAsync(signedAnswerBytes, WebSocketMessageType.Text, true, CancellationToken.None).Wait();



			Console.WriteLine("Press ctrl-c to exit.");

			// Ctrl-c will gracefully exit the call at any point.
			ManualResetEvent exitMre = new ManualResetEvent(false);
			Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
			{
				e.Cancel = true;
				exitMre.Set();
			};

			// Wait for a signal saying the call failed, was cancelled with ctrl-c or completed.
			exitMre.WaitOne();
		}

		private async static Task<(RTCPeerConnection, string)> CreatePeerConnection(string offer)
		{
			var iceServersArray = (TomlArray)confModel["ICEServers"];
			List<RTCIceServer> iceServers = new List<RTCIceServer>();
			foreach (var iceServerEntry in iceServersArray)
			{
				var iceServerTable = (TomlTable)iceServerEntry;
				if (!(iceServerTable.ContainsKey("Username")))
				{
					iceServers.Add(new RTCIceServer
					{
						urls = (string)((TomlArray)iceServerTable["URLs"])[0]
					});
				}
				else
				{
					iceServers.Add(new RTCIceServer
					{
						urls = (string)((TomlArray)iceServerTable["URLs"])[0],
						username = (string)iceServerTable["Username"],
						credential = (string)iceServerTable["Credential"]
					});
				}
			}
			RTCConfiguration config = new RTCConfiguration
			{
				iceServers = iceServers
			};
			var pc = new RTCPeerConnection(config);
			pc.ondatachannel += (rdc) =>
			{
				rdc.onopen += () => logger.LogDebug($"Data channel {rdc.label} opened.");
				rdc.onclose += () => logger.LogDebug($"Data channel {rdc.label} closed.");
				rdc.onmessage += (datachan, type, data) =>
				{
					switch (type)
					{
						case DataChannelPayloadProtocols.WebRTC_Binary_Empty:
						case DataChannelPayloadProtocols.WebRTC_String_Empty:
							logger.LogInformation($"Data channel {datachan.label} empty message type {type}.");
							break;

						case DataChannelPayloadProtocols.WebRTC_Binary:
							string jsSha256 = DoJavscriptSHA256(data);
							logger.LogInformation($"Data channel {datachan.label} received {data.Length} bytes, js mirror sha256 {jsSha256}.");
							rdc.send(jsSha256);

							if (_loadTestCount > 0)
							{
								DoLoadTestIteration(rdc, _loadTestPayloadSize);
								_loadTestCount--;
							}

							break;

						case DataChannelPayloadProtocols.WebRTC_String:
							var msg = Encoding.UTF8.GetString(data);
							logger.LogInformation($"Data channel {datachan.label} message {type} received: {msg}.");

							var loadTestMatch = Regex.Match(msg, @"^\s*(?<sendSize>\d+)\s*x\s*(?<testCount>\d+)");

							if (loadTestMatch.Success)
							{
								uint sendSize = uint.Parse(loadTestMatch.Result("${sendSize}"));
								_loadTestCount = int.Parse(loadTestMatch.Result("${testCount}"));
								_loadTestCount = (_loadTestCount <= 0 || _loadTestCount > MAX_LOADTEST_COUNT) ? MAX_LOADTEST_COUNT : _loadTestCount;
								_loadTestPayloadSize = (sendSize > pc.sctp.maxMessageSize) ? pc.sctp.maxMessageSize : sendSize;

								logger.LogInformation($"Starting data channel binary load test, payload size {sendSize}, test count {_loadTestCount}.");
								DoLoadTestIteration(rdc, _loadTestPayloadSize);
								_loadTestCount--;
							}
							else
							{
								// Do a string echo.
								rdc.send($"echo: {msg}");
							}
							break;
					}
				};
			};

			var dc = await pc.createDataChannel("data", null);

			pc.onconnectionstatechange += (state) =>
			{
				logger.LogDebug($"Peer connection state change to {state}.");

				if (state == RTCPeerConnectionState.failed)
				{
					pc.Close("ice disconnection");
				}
			};

			// Diagnostics.
			//pc.OnReceiveReport += (re, media, rr) => logger.LogDebug($"RTCP Receive for {media} from {re}\n{rr.GetDebugSummary()}");
			//pc.OnSendReport += (media, sr) => logger.LogDebug($"RTCP Send for {media}\n{sr.GetDebugSummary()}");
			//pc.GetRtpChannel().OnStunMessageReceived += (msg, ep, isRelay) => logger.LogDebug($"STUN {msg.Header.MessageType} received from {ep}.");
			pc.oniceconnectionstatechange += (state) => logger.LogDebug($"ICE connection state change to {state}.");
			pc.onsignalingstatechange += () => logger.LogDebug($"Signalling state changed to {pc.signalingState}.");
			RTCSessionDescriptionInit.TryParse(offer, out var offerS);
			pc.setRemoteDescription(offerS);
			string answer="";
			if(pc.signalingState==RTCSignalingState.have_remote_offer){
				var answerS = pc.createAnswer();
				await pc.setLocalDescription(answerS);
				Console.WriteLine(answerS.toJSON());
				answer=answerS.toJSON();
			}

			return (pc, answer);
		}

		private static void DoLoadTestIteration(RTCDataChannel dc, uint payloadSize)
		{
			var rndBuffer = new byte[payloadSize];
			Crypto.GetRandomBytes(rndBuffer);
			logger.LogInformation($"Data channel sending {payloadSize} random bytes, hash {DoJavscriptSHA256(rndBuffer)}.");
			dc.send(rndBuffer);
		}

		/// <summary>
		/// The Javascript hash function only allows a maximum input of 65535 bytes. In order to hash
		/// larger buffers for testing purposes the buffer is split into 65535 slices and then the hashes
		/// of each of the slices hashed.
		/// </summary>
		/// <param name="buffer">The buffer to perform the Javascript SHA256 hash of hashes on.</param>
		/// <returns>A hex string of the resultant hash.</returns>
		private static string DoJavscriptSHA256(byte[] buffer)
		{
			int iters = (buffer.Length <= JAVASCRIPT_SHA256_MAX_IN_SIZE) ? 1 : buffer.Length / JAVASCRIPT_SHA256_MAX_IN_SIZE;
			iters += (buffer.Length > iters * JAVASCRIPT_SHA256_MAX_IN_SIZE) ? 1 : 0;

			byte[] hashOfHashes = new byte[iters * SHA256_OUTPUT_SIZE];

			for (int i = 0; i < iters; i++)
			{
				int startPosn = i * JAVASCRIPT_SHA256_MAX_IN_SIZE;
				int length = JAVASCRIPT_SHA256_MAX_IN_SIZE;
				length = (startPosn + length > buffer.Length) ? buffer.Length - startPosn : length;

				var slice = new ArraySegment<byte>(buffer, startPosn, length);

				using (var sha256 = SHA256.Create())
				{
					Buffer.BlockCopy(sha256.ComputeHash(slice.ToArray()), 0, hashOfHashes, i * SHA256_OUTPUT_SIZE, SHA256_OUTPUT_SIZE);
				}
			}

			using (var sha256 = SHA256.Create())
			{
				return sha256.ComputeHash(hashOfHashes).HexStr();
			}
		}

		/// <summary>
		/// Adds a console logger. Can be omitted if internal SIPSorcery debug and warning messages are not required.
		/// </summary>
		private static Microsoft.Extensions.Logging.ILogger AddConsoleLogger()
		{
			var seriLogger = new LoggerConfiguration()
				.Enrich.FromLogContext()
				.MinimumLevel.Is(Serilog.Events.LogEventLevel.Debug)
				.WriteTo.Console()
				.CreateLogger();
			var factory = new SerilogLoggerFactory(seriLogger);
			SIPSorcery.LogFactory.Set(factory);
			return factory.CreateLogger<Program>();
		}
	}
}
