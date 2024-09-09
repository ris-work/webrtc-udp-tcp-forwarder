// See https://aka.ms/new-console-template for more information
using Terminal.Gui;
using Tomlyn.Model;
using Tomlyn;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Resources;
using System.Management.Automation;
using Microsoft.VisualBasic.FileIO;
using System.Text.Json.Serialization;


var a = Assembly.GetExecutingAssembly();
Console.WriteLine(a);
if (args.Length > 0)
{
    StartConfig.Filename = args[0];
}
Application.Run<RV.WebRTCForwarders.Window>().Dispose();


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

public static class Utils
{
    [JsonSourceGenerationOptions(IncludeFields = true)]
    public class ConfigOut
    {
        [JsonInclude] public string WebRtcForwarderConfigurationFileName = "";
        [JsonInclude] public string WireguardConfigName = "";
        [JsonInclude] public string ServiceConfigXmlFileName = "";
        [JsonInclude] public string ServicePowershellScript = "";
        public TomlTable ToTomlTable()
        {
            return new TomlTable
            {
                ["WebRtcForwarderConfigurationFileName"] = WebRtcForwarderConfigurationFileName,
                ["WireguardConfigName"] = WireguardConfigName,
                ["ServiceConfigXmlFileName"] = ServiceConfigXmlFileName,
                ["ServicePowershellScript"] = ServicePowershellScript
            };
        }
    }
    public static void Associate() {
        string ROOT = Path.Combine(SpecialDirectories.ProgramFiles, "rv", "rvtunsvc") ;
        string ScriptPrelude = "$exename = 'configinstaller.exe'\r\n" +
            "$extension = '.rvtunnelconfiguration'\r\n" +
            "$iconpath = '{ROOT}'\r\n" +
            "$formatdesc = 'Tunnel Configuration File (ZIP, encrypted)'\r\n" +
            "$appname = 'Tunnel Configuration Installer'\r\n";
        var a = Assembly.GetExecutingAssembly();
        var Script = new StreamReader(a.GetManifestResourceStream("ui.scripts.assoc.ps1")).ReadToEnd();
        var PSH = PowerShell.Create();
        
        PSH.AddScript(ScriptPrelude + Script);

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
