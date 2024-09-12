using System.Management.Automation;
using System.Reflection;
using Terminal.Gui;
using ICSharpCode.SharpZipLib;
using System.IO.Compression;
using RV.WebRTCForwarders;
using ICSharpCode.SharpZipLib.Zip;
using System.Text.Json.Serialization;
using Tomlyn.Model;
using Microsoft.VisualBasic.FileIO;
using System.Diagnostics;

public static class Config {
    ///public static string InstallationRoot = Path.Combine(SpecialDirectories.ProgramFiles, "rv", "rvtunsvc");
    public static string InstallationRoot = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
}

public static class ConfigInstaller
{
    public static int Main(string[] args)
    {
        // See https://aka.ms/new-console-template for more information
        Terminal.Gui.Application.Init();
        if (args.Length == 0)
        {
            MessageBox.Query("Association", "Associating files with myself, an argument is necessary otherwise.", "Ok");
            return 1;
        }
        MessageBox.Query("Installation", $"{Config.InstallationRoot}", "Ok");
        EnterKeyForm EKF = new EnterKeyForm();
        Application.Run(EKF);

        ICSharpCode.SharpZipLib.Zip.ZipInputStream Z;
        ICSharpCode.SharpZipLib.Zip.ZipFile ZF;

        try
        {
            //Z = new ICSharpCode.SharpZipLib.Zip.ZipInputStream(new FileStream(args[0], FileMode.Open));
            ZF = new ICSharpCode.SharpZipLib.Zip.ZipFile(args[0]);
            ZF.Password = EKF.Key;

        }
        catch (Exception ex)
        {
            MessageBox.Query("Zip Error", $"{ex.ToString()}");
            return 1;
        }

        try
        {
            string FileList = "";
            foreach (ZipEntry item in ZF)
            {
                FileList += item.Name + Environment.NewLine;
            }
            MessageBox.Query("File List", FileList, "Ok");
            var config_file = ZF.GetInputStream(ZF.GetEntry("config.toml"));
            
            string configuration = (new StreamReader(config_file)).ReadToEnd();
            MessageBox.Query("Config", $"{configuration}", "OK");
            ConfigIn ci = ConfigIn.FromTomlTable(Tomlyn.Toml.ToModel(configuration));
            MessageBox.Query("Config Read: ", $"{ci.PortNumber}", "OK");
            var TunnelsRoot = Path.Combine(Config.InstallationRoot, "tunnels");
            Directory.CreateDirectory(Path.Combine(TunnelsRoot, ci.PortNumber.ToString()));
            FastZip FZ = new FastZip() {
                Password = EKF.Key
            };
            try
            {
                FZ.ExtractZip(args[0], Path.Combine(TunnelsRoot, ci.PortNumber.ToString()), ".*");
            } catch (Exception E) {
                MessageBox.Query("Exception when extracting", $"{E.ToString()}\r\n{E.StackTrace}");
            }

            try
            {
                MessageBox.Query("Information", "Trying to register a Windows (R) service...", "Ok");
                ProcessStartInfo PSI_WINSW = new ProcessStartInfo()
                {
                    FileName = "winsw.exe",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };
                PSI_WINSW.ArgumentList.Add("install");
                PSI_WINSW.ArgumentList.Add(Path.Combine(TunnelsRoot, ci.PortNumber.ToString(), "rvtunsvc.xml"));
                var PS_WINSW = new System.Diagnostics.Process();
                PS_WINSW.StartInfo = PSI_WINSW;
                PS_WINSW.Start();
                ProcessStartInfo PSI_WINSW_STARTI = new ProcessStartInfo()
                {
                    FileName = "winsw.exe",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };
                PSI_WINSW_STARTI.ArgumentList.Add("start");
                PSI_WINSW_STARTI.ArgumentList.Add(Path.Combine(TunnelsRoot, ci.PortNumber.ToString(), "rvtunsvc.xml"));
                var PS_WINSW_START = new System.Diagnostics.Process();
                PS_WINSW_START.StartInfo = PSI_WINSW_STARTI;
                PS_WINSW_START.Start();
                ProcessStartInfo PSI_WINSW_REFRESHI = new ProcessStartInfo()
                {
                    FileName = "winsw.exe",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };
                PSI_WINSW_REFRESHI.ArgumentList.Add("refresh");
                PSI_WINSW_REFRESHI.ArgumentList.Add(Path.Combine(TunnelsRoot, ci.PortNumber.ToString(), "rvtunsvc.xml"));
                var PS_WINSW_REFRESH = new System.Diagnostics.Process();
                PS_WINSW_REFRESH.StartInfo = PSI_WINSW_REFRESHI;
                PS_WINSW_REFRESH.Start();
                var PS_WINSW_stderr = PS_WINSW.StandardError.ReadToEnd() + Environment.NewLine 
                    + PS_WINSW_START.StandardError.ReadToEnd() + Environment.NewLine 
                    + PS_WINSW_REFRESH.StandardError.ReadToEnd();
                var PS_WINSW_stdout = PS_WINSW.StandardOutput.ReadToEnd() + Environment.NewLine
                    + PS_WINSW_START.StandardOutput.ReadToEnd() + Environment.NewLine
                    + PS_WINSW_REFRESH.StandardOutput.ReadToEnd();
                MessageBox.Query("Information", $"WinSW returned (stdout, stderr): \r\n" +
                    $" {PS_WINSW_stdout}, {PS_WINSW_stderr}", "Ok");
            }
            catch (Exception E)
            {
                MessageBox.Query("WinSW failed", $"{E.ToString()}\r\n{E.StackTrace}");
            }

            try
            {
                MessageBox.Query("Information", "Trying to install WireGuard configuration...", "Ok");
                ProcessStartInfo PSI_WG_INST = new ProcessStartInfo()
                {
                    FileName = "wireguard.exe",
                    UseShellExecute = true,
                };
                PSI_WG_INST.ArgumentList.Add("/installtunnelservice");
                PSI_WG_INST.ArgumentList.Add(Path.Combine(TunnelsRoot, ci.PortNumber.ToString(), $"wg.rv.{ci.PortNumber.ToString()}.conf"));
                var PS_WG_INST = new System.Diagnostics.Process();
                PS_WG_INST.StartInfo = PSI_WG_INST;
                Process.Start(PSI_WG_INST);
                MessageBox.Query("Information", $"Ran: \r\n{PSI_WG_INST.FileName} {PSI_WG_INST.Arguments} as {PSI_WG_INST.UserName}", "Quit");
                MessageBox.Query("Information", "WireGuard tunnel installation attempt completed.", "Quit");
            }catch(Exception E)
            {
                MessageBox.Query("Wireguard tunnel installation exception", $"{ E.ToString() }\r\n{E.StackTrace}");
            }

        }
        catch (Exception E)
        {
            MessageBox.Query("Exception", $"{E.ToString()}\r\n{E.StackTrace}");
        }

        return 0;

    }
}


[JsonSourceGenerationOptions(IncludeFields = true)]
public class ConfigIn
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
    public static ConfigIn FromTomlTable(TomlTable TT) {
        return new ConfigIn
        {
            WebRtcForwarderConfigurationFileName = (string)TT["WebRtcForwarderConfigurationFileName"],
            WireguardConfigName = (string)TT["WireguardConfigName"],
            ServicePowershellScript = (string)TT["ServicePowershellScript"],
            ServiceConfigXmlFileName = (string)TT["ServiceConfigXmlFileName"],
            PortNumber = (int)((long)TT["PortNumber"]),
        };
    }
}