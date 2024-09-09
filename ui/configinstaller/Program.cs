using System.Management.Automation;
using System.Reflection;
using Terminal.Gui;
using ICSharpCode.SharpZipLib;
using System.IO.Compression;
using RV.WebRTCForwarders;
using ICSharpCode.SharpZipLib.Zip;
using System.Text.Json.Serialization;
using Tomlyn.Model;
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
            MessageBox.Query("Config Read: ", $"{ci.ServicePowershellScript}", "OK");
        }
        catch (Exception E)
        {
            MessageBox.Query("Exception", $"{E.ToString()}");
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
    public static ConfigIn FromTomlTable(TomlTable TT) {
        return new ConfigIn
        {
            WebRtcForwarderConfigurationFileName = (string)TT["WebRtcForwarderConfigurationFileName"],
            WireguardConfigName = (string)TT["WireguardConfigName"],
            ServicePowershellScript = (string)TT["ServicePowershellScript"],
            ServiceConfigXmlFileName = (string)TT["ServiceConfigXmlFileName"]
        };
    }
}