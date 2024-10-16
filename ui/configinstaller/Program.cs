﻿using System.Management.Automation;
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
using System.Security.Cryptography;
using System.Text;
using Humanizer;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Isopoh.Cryptography.Argon2;


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
        while (true)
        {
            try
            {
                if (args.Length == 0)
                {
                    MessageBox.Query("Association", "Associating files with myself, an argument is necessary otherwise.", "Ok");
                    return 1;
                }
                Console.Title = $"Installing: {Path.GetFileName(args[0])} ({args[0]})";
                MessageBox.Query("Installation", $"{Config.InstallationRoot}", "Ok");
                EnterKeyForm EKF = new EnterKeyForm();
                Application.Run(EKF);

                ICSharpCode.SharpZipLib.Zip.ZipInputStream Z;
                ICSharpCode.SharpZipLib.Zip.ZipFile ZF;
                var ArConf = new Argon2Config() { HashLength = 48, Lanes = 4, MemoryCost = 65536, Password = Encoding.UTF8.GetBytes(EKF.Key) };
                ArConf.Salt = Encoding.UTF8.GetBytes("RVTunnelSys");
                var ArConf2 = new Argon2Config() { HashLength = 48, Lanes = 4, MemoryCost = 65536, Password = Encoding.UTF8.GetBytes(EKF.Key) };
                ArConf2.Salt = Encoding.UTF8.GetBytes("RVTunnelSys");
                var PBKDH = Convert.FromBase64String(Argon2.Hash(ArConf).Split("$").Last());
                var PBKDH_IV = PBKDH.Skip(32).Take(16).ToArray();
                var PBKDH_KEY = PBKDH.Take(32).ToArray();
                MessageBox.Query("Key input:", $"{Argon2.Hash(ArConf).Split("$").Last()}", "Ok");
                var AesOutStreamConf = Aes.Create();
                AesOutStreamConf.KeySize = 256;
                var pwKey = KeyDerivation.Pbkdf2(EKF.Key, Encoding.UTF8.GetBytes("RVTunSvc"), KeyDerivationPrf.HMACSHA256, 100000, 32);
                AesOutStreamConf.Key = PBKDH_KEY;
                var pwIV = KeyDerivation.Pbkdf2(EKF.Key, Encoding.UTF8.GetBytes("RVTunSvc"), KeyDerivationPrf.HMACSHA256, 10000, 16);
                AesOutStreamConf.IV = PBKDH_IV;
                ICryptoTransform CTT = AesOutStreamConf.CreateDecryptor();


                var CSI = new CryptoStream(File.Open(args[0], FileMode.Open), CTT, CryptoStreamMode.Read);
                MemoryStream MSI = new();
                CSI.CopyTo(MSI);

                try
                {

                    //Z = new ICSharpCode.SharpZipLib.Zip.ZipInputStream(new FileStream(args[0], FileMode.Open));
                    ZF = new ICSharpCode.SharpZipLib.Zip.ZipFile(MSI);
                    ZF.Password = EKF.Key;

                }
                catch (Exception ex)
                {
                    MessageBox.Query("Zip Error", $"{ex.ToString()}");
                    return 1;
                }

                try
                {
                    string DateFileName = DateTime.Now.ToString("o").Replace(":", "_");
                    string MessagesLog = "";
                    string FileList = "";
                    foreach (ZipEntry item in ZF)
                    {
                        FileList += item.Name + Environment.NewLine;
                    }
                    MessagesLog += $"File List{Environment.NewLine}" +  FileList + $"Ok{Environment.NewLine}";
                    MessageBox.Query($"File List{Environment.NewLine}", FileList , $"Ok{Environment.NewLine}");
                    var config_file = ZF.GetInputStream(ZF.GetEntry("config.toml"));

                    string configuration = (new StreamReader(config_file)).ReadToEnd();
                    MessagesLog += $"Config {configuration}" + $"OK{Environment.NewLine}";
                    ConfigIn ci = ConfigIn.FromTomlTable(Tomlyn.Toml.ToModel(configuration));
                    MessagesLog += "Config Read: " + $"{ci.PortNumber}"+ $"OK{Environment.NewLine}";
                    var TunnelsRoot = Path.Combine(Config.InstallationRoot, "tunnels");
                    Directory.CreateDirectory(Path.Combine(TunnelsRoot, ci.PortNumber.ToString()));
                    FastZip FZ = new FastZip()
                    {
                        Password = EKF.Key
                    };


                    try
                    {
                        FZ.ExtractZip((Stream)MSI, Path.Combine(TunnelsRoot, ci.PortNumber.ToString()), FastZip.Overwrite.Always, (_) => true, ".*", ".*", true, true);
                    }
                    catch (Exception E)
                    {
                        MessagesLog += "Exception when extracting" + $"{E.ToString()}\r\n{E.StackTrace}{Environment.NewLine}";
                    }
                    string[] verbs = new[] { "install", "start", "refresh" };
                    (string,string)[] servicefiles = new[] {
                        ("RV Tunnel Service", "rvtunsvc.xml"),
                        ("Address Filtered Forwarder", "aff.xml"), 
                        ("ICMP echo request", "icmp.xml") 
                    };
                    string Messages = "";
                    foreach((string, string) servicefile in servicefiles) {
                        foreach (string verb in verbs) {
                            System.Console.WriteLine($"Attempt: {servicefile.Item1}, {servicefile.Item2}, {verb}");
                            try
                            {
                                ProcessStartInfo PSI_WINSW = new ProcessStartInfo()
                                {
                                    FileName = "winsw.exe",
                                    RedirectStandardError = true,
                                    RedirectStandardOutput = true,
                                };
                                PSI_WINSW.ArgumentList.Add(verb);
                                PSI_WINSW.ArgumentList.Add(Path.Combine(TunnelsRoot, ci.PortNumber.ToString(), servicefile.Item2));
                                var PS_WINSW = new System.Diagnostics.Process();
                                PS_WINSW.StartInfo = PSI_WINSW;
                                PS_WINSW.Start();
                                string PS_WINSW_STDOUT = PS_WINSW.StandardOutput.ReadToEnd();
                                string PS_WINSW_STDERR = PS_WINSW.StandardError.ReadToEnd();
                                PS_WINSW.WaitForExit();
                                Messages += $"{PS_WINSW_STDOUT}, {PS_WINSW_STDERR}{Environment.NewLine}";
                            }
                            catch(Exception E)
                            {
                                Messages += $"Exception: {E.ToString()}, {E.StackTrace}{Environment.NewLine}";
                            }
                        }
                    }
                    Messages += "Press Enter/[Esc] to continue...";
                    MessageBox.Query("Messages", Messages, "Ok");
                    MessagesLog += Messages;
                    /*
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
                        string PS_WINSW_STDOUT = PS_WINSW.StandardOutput.ReadToEnd();
                        string PS_WINSW_STDERR = PS_WINSW.StandardError.ReadToEnd();
                        PS_WINSW.WaitForExit();
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
                        var PS_WINSW_START_STDOUT = PS_WINSW_START.StandardOutput.ReadToEnd();
                        var PS_WINSW_START_STDERR = PS_WINSW_START.StandardError.ReadToEnd();
                        PS_WINSW_START.WaitForExit();
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
                        string PS_WINSW_REFRESH_STDOUT = PS_WINSW_REFRESH.StandardOutput.ReadToEnd();
                        string PS_WINSW_REFRESH_STDERR = PS_WINSW_REFRESH.StandardError.ReadToEnd();
                        PS_WINSW_REFRESH.WaitForExit();
                        var PS_WINSW_stderr = PS_WINSW_STDERR + Environment.NewLine
                            + PS_WINSW_START_STDERR + Environment.NewLine
                            + PS_WINSW_REFRESH_STDERR;
                        var PS_WINSW_stdout = PS_WINSW_STDOUT + Environment.NewLine
                            + PS_WINSW_START_STDOUT + Environment.NewLine
                            + PS_WINSW_REFRESH_STDOUT;
                        MessageBox.Query("Information", $"WinSW returned (stdout, stderr): \r\n" +
                            $" {PS_WINSW_stdout}, {PS_WINSW_stderr}", "Ok");
                    }
                    catch (Exception E)
                    {
                        MessageBox.Query("WinSW failed", $"{E.ToString()}\r\n{E.StackTrace}");
                    }
                    try
                    {
                        MessageBox.Query("Information", "Trying to register a Windows (R) service (AFF)...", "Ok");
                        ProcessStartInfo PSI_WINSWP = new ProcessStartInfo()
                        {
                            FileName = "winsw.exe",
                            RedirectStandardError = true,
                            RedirectStandardOutput = true,
                        };
                        PSI_WINSWP.ArgumentList.Add("install");
                        PSI_WINSWP.ArgumentList.Add(Path.Combine(TunnelsRoot, ci.PortNumber.ToString(), "aff.xml"));
                        var PS_WINSWP = new System.Diagnostics.Process();
                        PS_WINSWP.StartInfo = PSI_WINSWP;
                        PS_WINSWP.Start();
                        string PS_WINSW_STDOUT = PS_WINSWP.StandardOutput.ReadToEnd();
                        string PS_WINSW_STDERR = PS_WINSWP.StandardError.ReadToEnd();
                        PS_WINSWP.WaitForExit();
                        ProcessStartInfo PSI_WINSW_STARTPI = new ProcessStartInfo()
                        {
                            FileName = "winsw.exe",
                            RedirectStandardError = true,
                            RedirectStandardOutput = true,
                        };
                        PSI_WINSW_STARTPI.ArgumentList.Add("start");
                        PSI_WINSW_STARTPI.ArgumentList.Add(Path.Combine(TunnelsRoot, ci.PortNumber.ToString(), "aff.xml"));
                        var PS_WINSW_STARTP = new System.Diagnostics.Process();
                        PS_WINSW_STARTP.StartInfo = PSI_WINSW_STARTPI;
                        PS_WINSW_STARTP.Start();
                        var PS_WINSW_START_STDOUT = PS_WINSW_STARTP.StandardOutput.ReadToEnd();
                        var PS_WINSW_START_STDERR = PS_WINSW_STARTP.StandardError.ReadToEnd();
                        PS_WINSW_STARTP.WaitForExit();
                        ProcessStartInfo PSI_WINSW_REFRESHPI = new ProcessStartInfo()
                        {
                            FileName = "winsw.exe",
                            RedirectStandardError = true,
                            RedirectStandardOutput = true,
                        };
                        PSI_WINSW_REFRESHPI.ArgumentList.Add("refresh");
                        PSI_WINSW_REFRESHPI.ArgumentList.Add(Path.Combine(TunnelsRoot, ci.PortNumber.ToString(), "aff.xml"));
                        var PS_WINSW_REFRESHP = new System.Diagnostics.Process();
                        PS_WINSW_REFRESHP.StartInfo = PSI_WINSW_REFRESHPI;
                        PS_WINSW_REFRESHP.Start();
                        string PS_WINSW_REFRESH_STDOUT = PS_WINSW_REFRESHP.StandardOutput.ReadToEnd();
                        string PS_WINSW_REFRESH_STDERR = PS_WINSW_REFRESHP.StandardError.ReadToEnd();
                        PS_WINSW_REFRESHP.WaitForExit();
                        var PS_WINSW_stderr = PS_WINSW_STDERR + Environment.NewLine
                            + PS_WINSW_START_STDERR + Environment.NewLine
                            + PS_WINSW_REFRESH_STDERR;
                        var PS_WINSW_stdout = PS_WINSW_STDOUT + Environment.NewLine
                            + PS_WINSW_START_STDOUT + Environment.NewLine
                            + PS_WINSW_REFRESH_STDOUT;
                        MessageBox.Query("Information", $"WinSW returned (stdout, stderr): \r\n" +
                            $" {PS_WINSW_stdout}, {PS_WINSW_stderr}", "Ok");
                    }
                    catch (Exception E)
                    {
                        MessageBox.Query("Maybe AFF not requested! Don't Panic!", $"{E.ToString()}\r\n{E.StackTrace}");
                    }
                    try
                    {
                        MessageBox.Query("Information", "Trying to register a Windows (R) service (ICMP)...", "Ok");
                        ProcessStartInfo PSI_WINSWICMP = new ProcessStartInfo()
                        {
                            FileName = "winsw.exe",
                            RedirectStandardError = true,
                            RedirectStandardOutput = true,
                        };
                        PSI_WINSWICMP.ArgumentList.Add("install");
                        PSI_WINSWICMP.ArgumentList.Add(Path.Combine(TunnelsRoot, ci.PortNumber.ToString(), "icmp.xml"));
                        var PS_WINSWICMP = new System.Diagnostics.Process();
                        PS_WINSWICMP.StartInfo = PSI_WINSWICMP;
                        PS_WINSWICMP.Start();
                        string PS_WINSW_STDOUT = PS_WINSWICMP.StandardOutput.ReadToEnd();
                        string PS_WINSW_STDERR = PS_WINSWICMP.StandardError.ReadToEnd();
                        PS_WINSWICMP.WaitForExit();
                        ProcessStartInfo PSI_WINSW_STARTPIICMP = new ProcessStartInfo()
                        {
                            FileName = "winsw.exe",
                            RedirectStandardError = true,
                            RedirectStandardOutput = true,
                        };
                        PSI_WINSW_STARTPIICMP.ArgumentList.Add("start");
                        PSI_WINSW_STARTPIICMP.ArgumentList.Add(Path.Combine(TunnelsRoot, ci.PortNumber.ToString(), "icmp.xml"));
                        var PS_WINSW_STARTPICMP = new System.Diagnostics.Process();
                        PS_WINSW_STARTPICMP.StartInfo = PSI_WINSW_STARTPIICMP;
                        PS_WINSW_STARTPICMP.Start();
                        var PS_WINSW_START_STDOUT = PS_WINSW_STARTPICMP.StandardOutput.ReadToEnd();
                        var PS_WINSW_START_STDERR = PS_WINSW_STARTPICMP.StandardError.ReadToEnd();
                        PS_WINSW_STARTPICMP.WaitForExit();
                        ProcessStartInfo PSI_WINSW_REFRESHPIICMP = new ProcessStartInfo()
                        {
                            FileName = "winsw.exe",
                            RedirectStandardError = true,
                            RedirectStandardOutput = true,
                        };
                        PSI_WINSW_REFRESHPIICMP.ArgumentList.Add("refresh");
                        PSI_WINSW_REFRESHPIICMP.ArgumentList.Add(Path.Combine(TunnelsRoot, ci.PortNumber.ToString(), "icmp.xml"));
                        var PS_WINSW_REFRESHPICMP = new System.Diagnostics.Process();
                        PS_WINSW_REFRESHPICMP.StartInfo = PSI_WINSW_REFRESHPIICMP;
                        PS_WINSW_REFRESHPICMP.Start();
                        string PS_WINSW_REFRESH_STDOUT = PS_WINSW_REFRESHPICMP.StandardOutput.ReadToEnd();
                        string PS_WINSW_REFRESH_STDERR = PS_WINSW_REFRESHPICMP.StandardError.ReadToEnd();
                        PS_WINSW_REFRESHPICMP.WaitForExit();
                        var PS_WINSW_stderr = PS_WINSW_STDERR + Environment.NewLine
                            + PS_WINSW_START_STDERR + Environment.NewLine
                            + PS_WINSW_REFRESH_STDERR;
                        var PS_WINSW_stdout = PS_WINSW_STDOUT + Environment.NewLine
                            + PS_WINSW_START_STDOUT + Environment.NewLine
                            + PS_WINSW_REFRESH_STDOUT;
                        MessageBox.Query("Information", $"WinSW returned (stdout, stderr): \r\n" +
                            $" {PS_WINSW_stdout}, {PS_WINSW_stderr}", "Ok");
                    }
                    catch (Exception E)
                    {
                        MessageBox.Query("Maybe ICMP not requested! Don't Panic!", $"{E.ToString()}\r\n{E.StackTrace}");
                    }
                    */
                    try
                    {
                        MessageBox.Query("Information", "Trying to Uninstall WireGuard configuration first...", "Ok");
                        ProcessStartInfo PSI_WG_UNINST = new ProcessStartInfo()
                        {
                            FileName = "wireguard.exe",
                            UseShellExecute = true,
                        };
                        PSI_WG_UNINST.ArgumentList.Add("/uninstalltunnelservice");
                        PSI_WG_UNINST.ArgumentList.Add(Path.Combine(TunnelsRoot, ci.PortNumber.ToString(), Path.GetFileNameWithoutExtension($"wg.rv.{ci.PortNumber.ToString()}.conf")));
                        var PS_WG_UNINST = new System.Diagnostics.Process();
                        PSI_WG_UNINST.UseShellExecute = true;
                        PS_WG_UNINST.StartInfo = PSI_WG_UNINST;
                        Process.Start(PSI_WG_UNINST);
                        MessagesLog += "Information: " + $"Ran: \r\n{PSI_WG_UNINST.FileName} {String.Join(" ", PSI_WG_UNINST.ArgumentList.ToArray())} as {PSI_WG_UNINST.UserName}" + $"Quit{Environment.NewLine}";
                        MessagesLog += "Information: " + "WireGuard tunnel uninstallation attempt completed (for reinstallation)." + $"Ok{Environment.NewLine}";
                    }
                    catch (Exception E)
                    {
                        MessagesLog += "Wireguard tunnel uninstallation exception: " + $"{E.ToString()}\r\n{E.StackTrace}{Environment.NewLine}";
                        //File.AppendAllText($"configinstaller-{DateTime.Now.ToString("O")}.log", MessagesLog);
                    }
                    try
                    {
                        MessagesLog += ("Information" + "Trying to install WireGuard configuration... " + "Ok");
                        ProcessStartInfo PSI_WG_INST = new ProcessStartInfo()
                        {
                            FileName = "wireguard.exe",
                            UseShellExecute = true,
                        };
                        PSI_WG_INST.ArgumentList.Add("/installtunnelservice");
                        PSI_WG_INST.ArgumentList.Add(Path.Combine(TunnelsRoot, ci.PortNumber.ToString(), $"wg.rv.{ci.PortNumber.ToString()}.conf"));
                        var PS_WG_INST = new System.Diagnostics.Process();
                        PSI_WG_INST.UseShellExecute = true;
                        PS_WG_INST.StartInfo = PSI_WG_INST;
                        Process.Start(PSI_WG_INST);
                        MessagesLog += "Information: " + $"Ran: \r\n{PSI_WG_INST.FileName} {String.Join(" ", PSI_WG_INST.Arguments)} as {PSI_WG_INST.UserName}" + "Quit";
                        MessagesLog += "Information: " + "WireGuard tunnel installation attempt completed. " + $"Quit ✔ {Environment.NewLine}";
                    }
                    catch (Exception E)
                    {
                        MessagesLog += "Wireguard tunnel installation exception: " + $"{E.ToString()}\r\n{E.StackTrace}{Environment.NewLine}";
                    }
                    File.AppendAllText($"configinstaller-{DateFileName}.log", MessagesLog);
                    break;

                }


                catch (Exception E)
                {
                    MessageBox.Query("Outer Exception", $"{E.ToString()}\r\n{E.StackTrace}");

                }
            }
            catch(Exception E)
            {
                MessageBox.ErrorQuery("Outermost block", $"Exception, critical: {E.ToString()}, {E.StackTrace}", "Retry");
            }
           
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