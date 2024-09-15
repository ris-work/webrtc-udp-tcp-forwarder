// See https://aka.ms/new-console-template for more information
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using Tomlyn;
using Tomlyn.Model;
Console.Title = "Address Filtered Forwarder";
Console.WriteLine("This program is there to port-forward to a destination only if the source matches the given subnet(s)");
string configurationfile = "AddressFilteredForwarderConfiguration.toml";
if(args.Length > 0)
{
    configurationfile = args[0];
}
string TomlIn = File.ReadAllText(configurationfile);
TomlTable Config = Toml.ToModel(TomlIn);
Dictionary<string, object> ConfigDict = Config.ToDictionary();
string DestinationAddress = (string)ConfigDict.GetValueOrDefault("DestinationAddress", "127.0.0.1");
bool DestinationIsAUnixSocket = (bool)ConfigDict.GetValueOrDefault("DestinationIsAUnixSocket", false); ;
int DestinationPort = ((int)(long)ConfigDict.GetValueOrDefault("DestinationPort", 0));
string[] AllowedSubnetsConf = (string[])((TomlArray)ConfigDict["AllowedSources"]).Select(a => (string)a!).ToArray();
string[] ListenAddresses = (string[])(((TomlArray)ConfigDict["ListenAddresses"]).Select(a => (string)a!).ToArray());
int ListenPort = (int)(long)ConfigDict["ListenPort"];

IPEndPoint[] IPE = ListenAddresses.Select(a => new IPEndPoint(IPAddress.Parse(a), ListenPort)).ToArray();
IPNetwork[] AllowedSubnets = AllowedSubnetsConf.Select(a => IPNetwork.Parse(a)).ToArray();
string DebugIPEParsing = String.Join(", ", IPE.Select(a => a.ToString()));
string DebugAllowedSubnetsParsing = String.Join(", ", AllowedSubnets.Select(a => a.ToString()));
Console.WriteLine($"IPE: {DebugIPEParsing}");
Console.WriteLine($"AllowedIPR: {DebugAllowedSubnetsParsing}");

if (!DestinationIsAUnixSocket)
{
    IPEndPoint dest = new IPEndPoint(IPAddress.Parse(DestinationAddress), DestinationPort);
    Console.WriteLine($"Destination: {DestinationAddress}, {DestinationPort}");


    var Threads = IPE.Select(a =>
    {
        try
        {
            Console.WriteLine($"Thread for: {a.Address}, {a.Port}");
            (new Thread(async () =>
            {
                Forwarder.BeginForwarding(a, dest, AllowedSubnets).GetAwaiter().GetResult();
            })).Start();
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: with {a.Address}, {a.Port}: {ex.ToString()}");
            return 1;
        }
    }).ToList();
}
else
{
    var UDSForwarderThreads = IPE.Select(a => {
        var ForwarderThread = new Thread(async () => {
            await Task.Yield();
            var server = new TcpListener(a.Address, a.Port);
            while (true)
            {
                try
                {
                    var C = await server.AcceptTcpClientAsync();
                    bool ClientIsInAllowedSubnets = AllowedSubnets.Any(a => a.Contains(((IPEndPoint)C.Client.RemoteEndPoint).Address));
                    if (ClientIsInAllowedSubnets)
                    {
                        Socket S = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                        S.Connect(new UnixDomainSocketEndPoint(DestinationAddress));
                        var NS = new NetworkStream(S, true);
                        await Forwarder.HandleClientGenericStream(C, NS, AllowedSubnets);
                    }
                }
                catch (Exception E)
                {
                    Console.WriteLine($"{a} => {DestinationAddress}: {E.Message}");
                }
                
            }
        });
        ForwarderThread.Start();
        return 1;
    }
    );
}

public static class Forwarder
{
    public static async Task<int> BeginForwarding(IPEndPoint e, IPEndPoint dest, IPNetwork[] AllowedSubnets)
    {
        try
        {
            var server = new TcpListener(e);
            server.Start();
            while (true)
            {
                try {
                    var C = server.AcceptTcpClientAsync() ;
                    await HandleClient(await C, dest, AllowedSubnets);
                }
                catch (Exception E)
                {
                    Console.WriteLine($"{e} => {dest}: {E.Message}");
                }
                Console.WriteLine("Waiting for next client...");

            }
        }
        catch (Exception E)
        {
            Console.WriteLine($"Exception with : {e.Address}, {e.Port} => {dest.Address}, {dest.Port}: {E.ToString()}");
        }
        return 0;
    }
    public static async Task HandleClient(TcpClient C, IPEndPoint dest, IPNetwork[] AN)
    {
        await Task.Yield();
        bool ClientIsInAllowedSubnets = AN.Any(a => a.Contains(((IPEndPoint)C.Client.RemoteEndPoint).Address));
        if (ClientIsInAllowedSubnets)
        {
            Console.WriteLine($"{dest}");
            TcpClient CD = new TcpClient(dest.Address.ToString(), dest.Port);
            byte[] bytesToDest = new byte[65536];
            byte[] bytesToClient = new byte[65536];
            Console.WriteLine($"New: {C.Client.RemoteEndPoint.ToString()}");
            NetworkStream NS = C.GetStream();
            NetworkStream DS = CD.GetStream();
            int iTD;
            int iTC;
            var fnB = async () =>
            {
                while ((iTD = await NS.ReadAsync(bytesToDest, CancellationToken.None)) != 0)
                {
                    Console.WriteLine("bytesToDest loop");
                    await DS.WriteAsync(bytesToDest, 0, iTD);
                    await DS.FlushAsync();
                    await Task.Yield();
                };
            };
            var fnA = async () =>
            {
                while ((iTC = await DS.ReadAsync(bytesToClient, CancellationToken.None)) != 0)
                {
                    Console.WriteLine("bytesToClient loop");
                    await NS.WriteAsync(bytesToClient, 0, iTC);
                    await NS.FlushAsync();
                    await Task.Yield();
                };
            };
            await Task.Yield();
            fnA(); fnB();
        }
        else
        {
            Console.WriteLine($"Access denied for {((IPEndPoint)C.Client.RemoteEndPoint).Address}");
            C.Close();
        }
        
    }
    public static async Task HandleClientGenericStream(TcpClient C, Stream dest, IPNetwork[] AN)
    {
        await Task.Yield();
        bool ClientIsInAllowedSubnets = AN.Any(a => a.Contains(((IPEndPoint)C.Client.RemoteEndPoint).Address));
        if (ClientIsInAllowedSubnets)
        {
            Console.WriteLine($"{dest}");
            byte[] bytesToDest = new byte[65536];
            byte[] bytesToClient = new byte[65536];
            Console.WriteLine($"New: {C.Client.RemoteEndPoint.ToString()}");
            NetworkStream NS = C.GetStream();
            int iTD;
            int iTC;
            var Cst = C.GetStream();
            var Dst = dest;
            var fnB = async () =>
            {
                await Dst.CopyToAsync(Cst);
            };
            var fnA = async () =>
            {
                await Cst.CopyToAsync(dest);
            };
            await Task.Yield();
            fnA(); fnB();
        }
        else
        {
            Console.WriteLine($"Access denied for {((IPEndPoint)C.Client.RemoteEndPoint).Address}");
            C.Close();
        }

    }
}