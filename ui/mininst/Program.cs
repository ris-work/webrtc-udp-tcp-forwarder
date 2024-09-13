// See https://aka.ms/new-console-template for more information
using Microsoft.VisualBasic.FileIO;
using System.Security.AccessControl;
using System.Security.Principal;

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
    File.Copy(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName, Path.Combine(root, "ui.exe"), true);
}
catch (Exception E)
{
    System.Console.WriteLine($"Exception: {E.ToString()}");
}
System.Console.WriteLine("Done, starting ui.exe...");
System.Diagnostics.Process.Start(Path.Combine(root, "ui.exe"));