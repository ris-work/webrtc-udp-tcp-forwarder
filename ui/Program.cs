// See https://aka.ms/new-console-template for more information
using Terminal.Gui;
using Tomlyn.Model;
using Tomlyn;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Resources;
using Microsoft.VisualBasic.FileIO;
using System.Text.Json.Serialization;
using System.Reflection.PortableExecutable;
using System.Security.Policy;
using System.Management.Automation;


var a = Assembly.GetExecutingAssembly();
//System
Console.WriteLine(a);
if(args.Length  == 0)
{
    Application.Run<RV.WebRTCForwarders.UtilsForm>().Dispose();
    return 0;
}
if (args.Length > 0)
{
    StartConfig.Filename = args[0];
}
Application.Run<RV.WebRTCForwarders.Window>().Dispose();
return 0;


public partial class IceServers
{
    public string[] URLs;
    public string Username;
    public string Credential;
}


public static class StartConfig
{
    public static string Filename = "sample.toml";
}

public static class Config
{
    public static string InstallationRoot = Path.Combine(SpecialDirectories.ProgramFiles, "rv", "rvtunsvc");
}
public static class Utils
{
    [JsonSourceGenerationOptions(IncludeFields = true)]
    public class ConfigOut
    {
        [JsonInclude] public string WebRtcForwarderConfigurationFileName = "";
        [JsonInclude] public string WireguardConfigName = "";
        [JsonInclude] public string ServiceConfigXmlFileName = "";
        [JsonInclude] public string ServicePowershellScript = "";
        [JsonInclude] public int PortNumber = 0;
        public TomlTable ToTomlTable()
        {
            return new TomlTable
            {
                ["WebRtcForwarderConfigurationFileName"] = WebRtcForwarderConfigurationFileName,
                ["WireguardConfigName"] = WireguardConfigName,
                ["ServiceConfigXmlFileName"] = ServiceConfigXmlFileName,
                ["ServicePowershellScript"] = ServicePowershellScript,
                ["PortNumber"] = PortNumber,
            };
        }
    }

    public class ForwarderConfigOut {
        public string Type = "";
        public string WebRTCMode = "";
        public string Address = "127.0.0.1";
        public string Port = "";
        public TomlArray ICEServers = new TomlArray() {
            new TomlTable()
            {
                ["URLs"] = new TomlArray()
                {
                    "stun:vz.al"
                }
            },
            new TomlTable()
            {
                ["URLs"] = new TomlArray()
                {
                    "stun:stun.l.google.com:19302"
                }
            }
        };
        public string PublishType = "ws";
        public string PublishEndpoint = "";
        public string PublishAuthType = "";
        public string PublishAuthUser = "";
        public string PublishAuthPass = "";
        public string PeerAuthType = "PSK";
        public string PeerPSK = "";

        public TomlTable ToTomlTable() {
            return new TomlTable()
            {
                ["Type"] = Type,
                ["WebRTCMode"] = WebRTCMode,
                ["Address"] = Address,
                ["Port"] = Port,
                ["PublishType"] = PublishType,
                ["PublishEndpoint"] = PublishEndpoint,
                ["PublishAuthType"] = PublishAuthType,
                ["PublishAuthUser"] = PublishAuthUser,
                ["PublishAuthPass"] = PublishAuthPass,
                ["PeerAuthType"] = PeerAuthType,
                ["PeerPSK"] = PeerPSK,
                ["ICEServers"] = ICEServers,
                
            };
        }
    }
    public static void Associate() {
        string ROOT = Path.Combine(SpecialDirectories.ProgramFiles, "rv", "rvtunsvc") ;
        var OldDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(ROOT);
        string ScriptPrelude = "$exename = 'configinstaller.exe'\r\n" +
            "$extension = '.rvtunnelconfiguration'\r\n" +
            $"$iconpath = 'configinstaller.exe,0'\r\n" +
            "$formatdesc = 'Tunnel Configuration File (ZIP, encrypted)'\r\n" +
            "$appname = 'Tunnel Configuration Installer'\r\n";
        var a = Assembly.GetExecutingAssembly();
        var Script = new StreamReader(a.GetManifestResourceStream("ui.scripts.assoc.ps1")).ReadToEnd();
        Thread T = (new Thread(() => {
            try
            {
                Directory.SetCurrentDirectory(ROOT);
                var PSH = PowerShell.Create(RunspaceMode.NewRunspace);
                //MessageBox.Query("Script", ScriptPrelude+Script, "Ok");
                PSH.AddScript($"$working_directory = '{ROOT}'\r\nSet-Location '{ROOT}'\r\n" + ScriptPrelude + "\r\n" + Script + "\r\necho \"Done\"");
                //MessageBox.Query("Association", "Association script will run now", "Ok");
                var output = PSH.Invoke();
                //MessageBox.Query("Association script", $"Script exited:\r\n{String.Join("", output.Select(e => e.ToString()))}");
                string aggout = output.Aggregate("", (a, b) => a +"\r\n"+ b.ToString(), agg => agg.ToString());
                Directory.SetCurrentDirectory(OldDir);
                File.WriteAllText($"{OldDir}\\instlog.log", aggout);
                
                File.WriteAllText($"{ROOT}\\instlog.log", aggout);
            }
            catch (Exception E)
            {
                MessageBox.Query("Exception - Association", E.ToString(), "Ok");
            }
        }));
        T.Start();
        Directory.SetCurrentDirectory(OldDir);

    }
    public static string MakeItLookLikeACdKey(string text)
    {
        char[] a = text.ToCharArray();
        string output = "";
        int i = 0;
        foreach (var character in a)
        {
            i++;
            output += character;
            if(i % 5 == 0)
            {
                output += "-";
            }
        }
        return output;
    }
    public static string MakeItNormalBase32(string text)
    {
        return text.ToUpperInvariant().Replace("-", String.Empty)+"======"; //It expects 6 chars padding for 26 characters, making it 32 bytes _in_.
    }
}
