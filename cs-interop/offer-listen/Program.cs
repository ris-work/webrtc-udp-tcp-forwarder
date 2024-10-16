﻿//-----------------------------------------------------------------------------
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
using System.Net.Sockets;
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
	public delegate void send(byte[] data);

	public struct UdpState
	{
		public UdpClient u;
		public IPEndPoint e;
	}

	class Program
	{
		private static string TrickleICEWorkaround = "";
		private static TomlTable? confModel = null;
		private const int WEBSOCKET_PORT = 8081;
		private const string STUN_URL = "stun:stun.sipsorcery.com";
		private const int JAVASCRIPT_SHA256_MAX_IN_SIZE = 65535;
		private const int SHA256_OUTPUT_SIZE = 32;
		private const int MAX_LOADTEST_COUNT = 100;

		private static Microsoft.Extensions.Logging.ILogger logger = NullLogger.Instance;

		private static uint _loadTestPayloadSize = 0;
		private static int _loadTestCount = 0;

		static send ToDC;
		static send ToOS;
		static Queue<byte[]> ToDCQueue;
		static Queue<byte[]> ToOSQueue;


		static int TimeSinceNoSendOS = 0;
		static int TimeSinceNoSendDC = 0;
		static long TimeoutCountMax = 0;

		[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
		[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
		static void Main(string[] args)
		{
			ToOSQueue = new Queue<byte[]>(32);
			ToDCQueue = new Queue<byte[]>(32);
			ToDC = (byte[] data) => { ToDCQueue.Enqueue(data); Console.WriteLine("Queued"); };
			ToOS = (byte[] data) => { ToOSQueue.Enqueue(data); };

			string config = File.ReadAllText(args[0]);
			var model = Toml.ToModel(config);
			confModel = model;

			Console.WriteLine("WebRTC Get Started Data Channel");

			logger = AddConsoleLogger();

			// Start web socket.
			Console.WriteLine("Starting web socket client...");
			var clientSock = new ClientWebSocket();
			//string socketPath = String.Join('/', ((string)model["PublishEndpoint"]).Split('/').Skip(2).ToArray());
			string webrtcMode = (string)model["WebRTCMode"];
			if(webrtcMode != "Offer") {
				System.Console.Error.WriteLine("Wrong TOOL: Wrong WebRTCMode");
				return;
			}
			string socketPathRaw = (string)model["PublishEndpoint"];
			string user = (string)model["PublishAuthUser"];
			string password = (string)model["PublishAuthPass"];
			string peerPSK = (string)model["PeerPSK"];
			TimeoutCountMax = ((long)model["TimeoutCountMax"]);
			IPAddress address = IPAddress.Parse((string)model["Address"]);
			int port = Int32.Parse((string)model["Port"]);
			IPEndPoint e = new IPEndPoint(address, port);
			IPEndPoint r = new IPEndPoint(IPAddress.Any, 0);
			(new Thread(() =>
			{
				UdpClient OS = new UdpClient(e);
				UdpState s = new UdpState();
				s.e = e;
				s.u = OS;
				//OS.Connect(e);
				byte[] data = OS.Receive(ref r);
				Program.ToDC(data);
				OS.Connect(r);
				//OS.BeginReceive(new AsyncCallback(UdpReceiveCallback), s);
				ToOS = (byte[] data) =>
				{
					OS.Send(data, data.Length);
					TimeSinceNoSendOS = 0;
				};
				while (ToOSQueue.TryDequeue(out var msg))
				{
					ToOS(msg);
				}
				while (true)
				{
					try
					{
						data = OS.Receive(ref r);
						Program.ToDC(data);
					}
					catch (Exception E)
					{
						Console.WriteLine(E);
						break;
					}
				}
			})).Start();
			(new Thread(() =>
			{
				while (true)
				{
					Thread.Sleep(1000);
					GC.Collect();
					TimeSinceNoSendDC++;
					TimeSinceNoSendOS++;
					if ((TimeSinceNoSendDC > TimeoutCountMax) ||
						(TimeSinceNoSendOS > TimeoutCountMax))
					{
						Console.WriteLine("Exiting due to inactivity...");
						Environment.Exit(0);
					}
				}
			})).Start();
			var jsonOptionsT = new JsonSerializerOptions()
			{
				TypeInfoResolver = TMC.Default,
				IncludeFields = true,
				UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
			};
			//string uriString = $"wss://{user}:{password}@{socketPath}";
			var jsonOptionsA = new JsonSerializerOptions()
			{
				TypeInfoResolver = AMC.Default,
				IncludeFields = true,
				UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
			};
			string uriString = $"{socketPathRaw}";
			byte[] peerPSKBytes = Encoding.UTF8.GetBytes(peerPSK);
			Console.WriteLine(uriString);
			string creds = $"{user}:{password}";
			string credsB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(creds));
			string authString = $"Basic {credsB64}";
			Uri uriWithAuth = new Uri(uriString);
			clientSock.Options.SetRequestHeader("Authorization", authString);
			clientSock.ConnectAsync(uriWithAuth, CancellationToken.None).Wait();
			byte[] answerSignedBytes = new byte[65536];
			//ArraySegment<byte> offerSignedBytesSegment = new ArraySegment<byte>(offerSignedBytes);
			var pcTask = CreatePeerConnection();
			pcTask.Wait();
			(var pc, var offer) = pcTask.Result;
			Console.WriteLine("offer");
			Console.WriteLine(offer);


			byte[] offerBytes = Encoding.UTF8.GetBytes(offer);
			string offerBase64 = Convert.ToBase64String(offerBytes);
			TimedMessage timedOffer = new TimedMessage(offerBase64);

			string timedOfferJson = JsonSerializer.Serialize(timedOffer, typeof(TimedMessage), jsonOptionsT);

			AuthenticatedMessage signedOffer = new AuthenticatedMessage(timedOfferJson, peerPSKBytes);

			string signedOfferJson = JsonSerializer.Serialize(signedOffer, typeof(AuthenticatedMessage), jsonOptionsA);
			byte[] signedOfferBytes = Encoding.UTF8.GetBytes(signedOfferJson);
			Console.WriteLine(signedOfferJson);
			clientSock.SendAsync(signedOfferBytes, WebSocketMessageType.Text, true, CancellationToken.None).Wait();
			var taskRecv = clientSock.ReceiveAsync(answerSignedBytes, CancellationToken.None);
			taskRecv.Wait();
			var result = taskRecv.Result;
			Console.WriteLine("Got answer string");
			var answerSignedJson = Encoding.UTF8.GetString(answerSignedBytes[0..result.Count]);
			Console.WriteLine(answerSignedJson);

			AuthenticatedMessage authenticatedAnswer = JsonSerializer.Deserialize<AuthenticatedMessage>(answerSignedJson, jsonOptionsA);

			string timedAnswerJson = authenticatedAnswer.GetMessage(peerPSKBytes);
			TimedMessage timedAnswer = JsonSerializer.Deserialize<TimedMessage>(timedAnswerJson, jsonOptionsT);
			string answerBase64 = timedAnswer.GetMessage();
			byte[] answerBytes = Convert.FromBase64String(answerBase64);
			string answer = Encoding.UTF8.GetString(answerBytes);
			Console.WriteLine(answer);

			RTCSessionDescriptionInit.TryParse(answer, out var answerS);
			pc.setRemoteDescription(answerS);


			/*
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
			*/



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

		[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
		private async static Task<(RTCPeerConnection, string)> CreatePeerConnection()
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
			var rdc = await pc.createDataChannel("data", new RTCDataChannelInit
			{
				ordered = false,
				maxRetransmits = 0

			});
			/*Program.ToDC = (byte[] data) =>
			{
				rdc.send(data);
				TimeSinceNoSendDC = 0;
			};
			Console.WriteLine("ToDC changed");
			while (ToDCQueue.TryDequeue(out var msg))
			{
				ToDC(msg);
			}*/
			rdc.onopen += () =>
			{
				logger.LogDebug($"Data channel {rdc.label} opened.");
				Program.ToDC = (byte[] data) =>
				{
					if (rdc.bufferedAmount < 4000000)
						rdc.send(data);
					TimeSinceNoSendDC = 0;
					//System.Console.WriteLine("ToDC");
				};
				Console.WriteLine("ToDC changed");
				while (ToDCQueue.TryDequeue(out var msg))
				{
					ToDC(msg);
				}
				TimeSinceNoSendDC = 0;
			};
			rdc.onclose += () => logger.LogDebug($"Data channel {rdc.label} closed.");
			rdc.onmessage += (datachan, type, data) =>
			{
				ToOS(data.ToArray());
			};

			//var dc = await pc.createDataChannel("data", null);

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
			pc.oniceconnectionstatechange += (state) => logger.LogDebug($"ICE connection state change to {state}, {pc.currentLocalDescription.sdp.RawString()}.");
			pc.onicegatheringstatechange += (state) =>
			{
				if (state == RTCIceGatheringState.complete)
				{
					logger.LogDebug($"ICE connection state change to {state}, {pc.localDescription.sdp}.");
					//pc.localDescription.Sdp += TrickleICEWorkaround;
					//pc.localDescription.sdp.ICECandidates.Add(c);
					logger.LogDebug($"ICE connection state change to {state}, {pc.localDescription.sdp}.");
				}
			};
			pc.onicecandidate += (c) =>
			{
				TrickleICEWorkaround += "\r\na=candidate:{c}";
				pc.addLocalIceCandidate(c);
				Console.WriteLine(c);
				if (pc.localDescription != null)
				{
					if (pc.localDescription.sdp.IceCandidates == null)
						pc.localDescription.sdp.IceCandidates = new List<String>();
					if (c != null)
						pc.localDescription.sdp.IceCandidates.Add(c.ToString());
				}
			};
			pc.onsignalingstatechange += () => logger.LogDebug($"Signalling state changed to {pc.signalingState}.");
			//RTCSessionDescriptionInit.TryParse(offer, out var offerS);
			//pc.setRemoteDescription(offerS);
			string offer = "";
			/*if (pc.signalingState == RTCSignalingState.have_remote_offer)
			{*/
			var offerS = pc.createOffer(null);
			await pc.setLocalDescription(offerS);
			await Task.Delay(1000);
			offer = JsonSerializer.Serialize(new MungedSDP()
			{
				type = "offer",
				sdp = pc.localDescription.sdp.ToString()
			}, MSDPC.Default.MungedSDP);
			Console.WriteLine(offer);
			//offer = offerS.toJSON();
			//}

			return (pc, offer);
		}
		public static void UdpReceiveCallback(IAsyncResult ar)
		{
			UdpClient u = ((UdpState)(ar.AsyncState)).u;
			IPEndPoint e = ((UdpState)(ar.AsyncState)).e;
			byte[] data = u.EndReceive(ar, ref e);
			System.Console.WriteLine("UDP Received");
			ToDC(data);
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
