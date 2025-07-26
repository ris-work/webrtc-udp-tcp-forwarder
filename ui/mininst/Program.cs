// See https://aka.ms/new-console-template for more information
using Microsoft.VisualBasic.FileIO;
using System.Collections;
using System.Security.AccessControl;
using System.Security.Principal;

Console.Title = "RV P2P E2E encrypted tunnel system installer";
System.Console.WriteLine("Minimal installer for RV Tunnel Services, (Ctrl+C) to exit");
var root = Path.Combine(SpecialDirectories.ProgramFiles, "rv", "rvtunsvc");
try
{
    Directory.CreateDirectory(Path.Combine(SpecialDirectories.ProgramFiles, "rv"));
}
catch (Exception _) { }
try
{
    Directory.CreateDirectory(Path.Combine(SpecialDirectories.ProgramFiles, "rv", "rvtunsvc"));
}
catch (Exception) { }
try
{
    Directory.CreateDirectory(Path.Combine(SpecialDirectories.ProgramFiles, "rv", "rvtunsvc", "tunnels"));
}
catch (Exception) { }
try
{
    DirectoryInfo DI = new DirectoryInfo(Path.Combine(SpecialDirectories.ProgramFiles, "rv", "rvtunsvc", "tunnels"));
    var DA2 = DI.GetAccessControl();
    var DA = new DirectorySecurity();
    DA.SetAccessRuleProtection(true, false);
    var FAAdmin = new FileSystemAccessRule("Administrators", FileSystemRights.FullControl, AccessControlType.Allow);
    var FACurrentUser = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User, FileSystemRights.FullControl, AccessControlType.Allow);
    var FASystem = new FileSystemAccessRule("SYSTEM", FileSystemRights.FullControl, AccessControlType.Allow);
    DA.AddAccessRule(FAAdmin);
    DA.AddAccessRule(FACurrentUser);
    DA.AddAccessRule(FASystem);
    DI.SetAccessControl(DA);
    Directory.CreateDirectory(root);
}
catch (Exception E)
{
    System.Console.WriteLine(E.ToString());
}
var HC = new HttpClient();
try
{
    var output_configinst = HC.GetStreamAsync("https://vz.al/chromebook/webrtc-udp-tcp-forwarder/uv/ui.exe").GetAwaiter().GetResult();
    var configinst_exe = File.Create(Path.Combine(root, "ui.exe"));
    output_configinst.CopyTo(configinst_exe);
    configinst_exe.Close();
    output_configinst.Close();
}
catch (Exception E)
{
    System.Console.WriteLine($"Exception: {E.ToString()}");
}
try
{
    var output_pf = HC.GetStreamAsync("https://vz.al/chromebook/webrtc-udp-tcp-forwarder/uv/AddressFilteredForwarder.exe").GetAwaiter().GetResult();
    var pf_exe = File.Create(Path.Combine(root, "AddressFilteredForwarder.exe"));
    output_pf.CopyTo(pf_exe);
    pf_exe.Close();
    output_pf.Close();
}
catch (Exception E)
{
    System.Console.WriteLine($"Exception: {E.ToString()}, {E.StackTrace}");
}
string Messages = "";
try
{
    HttpClient HC2 = new HttpClient();
    var Programs = new[] {
                        ("Accept(Answer)-Connect [Core]", "https://vz.al/chromebook/webrtc-udp-tcp-forwarder/uv/a-c.exe", "a-c.exe"),
                        ("Offer-Listen [Core]", "https://vz.al/chromebook/webrtc-udp-tcp-forwarder/uv/o-l.exe", "o-l.exe"),
                        ("Accept(Answer)-Connect [Named, UDP, Core]", "https://vz.al/chromebook/webrtc-udp-tcp-forwarder/uv/u-a-c.exe", "u-a-c.exe"),
                        ("Offer-Listen [Named, UDP, Core]", "https://vz.al/chromebook/webrtc-udp-tcp-forwarder/uv/u-o-l.exe", "u-o-l.exe"),
                        ("Accept(Answer)-Connect [Named, TCP, Core]", "https://vz.al/chromebook/webrtc-udp-tcp-forwarder/uv/t-a-c.exe", "t-a-c.exe"),
                        ("Offer-Listen [Named, TCP, Core]", "https://vz.al/chromebook/webrtc-udp-tcp-forwarder/uv/t-o-l.exe", "t-o-l.exe"),
                        ("Wscs [WebSockets]", "https://vz.al/chromebook/webrtc-udp-tcp-forwarder/uv/wscs.exe", "wscs.exe"),
                        ("WinSW [Core]", "https://github.com/winsw/winsw/releases/download/v3.0.0-alpha.11/WinSW-x64.exe", "winsw.exe"),
                        ("ConfigInstaller [Core]", "https://vz.al/chromebook/webrtc-udp-tcp-forwarder/uv/configinstaller.exe", "configinstaller.exe"),
                        ("Port Forwarder with Access Control", "https://vz.al/chromebook/webrtc-udp-tcp-forwarder/uv/AddressFilteredForwarder.exe", "AddressFilteredForwarder.exe"),
                    };
    foreach (var program in Programs)
    {
        try
        {
            Console.WriteLine($"Installing {program.Item1} from {program.Item2}...");
            var output = HC2.GetStreamAsync(program.Item2).GetAwaiter().GetResult();
            var out_exe = File.Create(Path.Combine(root, program.Item3));
            output.CopyTo(out_exe);
            output.Close();
            out_exe.Close();
        }
        catch (Exception E)
        {
            Messages += $"Exception while installing {program.Item1} (installation probably failed): {E.ToString()}, {E.StackTrace}{Environment.NewLine}";

        }
    }
    Messages += ("Done downloading, press [Esc], [Enter] or [Return] to continue...");
    Console.Error.WriteLine($"Messages: {Messages}");
}
catch (Exception E) {
    Console.Error.WriteLine($"Messages: {Messages}");
    Console.Error.WriteLine($"Outer exception: {E.StackTrace}, {E.ToString()}");
}

try
{
    // Backup all environment variables
    var envVars = Environment.GetEnvironmentVariables();
    var backupLines = new List<string>();
    foreach (DictionaryEntry entry in envVars)
    {
        backupLines.Add($"{entry.Key}={entry.Value}");
    }
    File.AppendAllLines("env_old", backupLines);

    // Retrieve and modify PATH
    var CurrentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
    var pathToAdd = root; // change this to the desired path

    var segments = CurrentPath.Split(Path.PathSeparator).ToList();
    if (!segments.Contains(pathToAdd))
    {
        segments.Add(pathToAdd);
        var newPath = string.Join(Path.PathSeparator, segments);

        // Persist to both user and system
        Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.User);
        Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.Machine);
    }

    // Dump updated environment to stdout
    foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
    {
        Console.WriteLine($"{entry.Key}={entry.Value}");
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"An error occurred: {ex.Message}");
}

System.Console.WriteLine("Done, starting ui.exe...");
System.Diagnostics.Process.Start(Path.Combine(root, "ui.exe"));