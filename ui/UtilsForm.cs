
//------------------------------------------------------------------------------

//  <auto-generated>
//      This code was generated by:
//        TerminalGuiDesigner v2.0.0.0
//      You can make changes to this file and they will not be overwritten when saving.
//  </auto-generated>
// -----------------------------------------------------------------------------



namespace RV.WebRTCForwarders {
    using Microsoft.VisualBasic;
    using Microsoft.VisualBasic.FileIO;
    using System.Configuration;
    using System.Net;
    using System.Reflection;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using System.Text;
    using Terminal.Gui;
    using WindowsFirewallHelper;
    
    
    public partial class UtilsForm {
        
        public UtilsForm() {
            InitializeComponent();
            instsoftware.ShadowStyle = ShadowStyle.Transparent;
            instsoftware.SetBorderStyle(LineStyle.Rounded );
            instsoftware.Border.BorderStyle = LineStyle.Single;
            var root = Path.Combine(SpecialDirectories.ProgramFiles, "rv", "rvtunsvc");
            //var root = Environment.CurrentDirectory;


            instsoftware.Accept += (_, _) => {
                MessageBox.Query("Directory", $"Will be installed in:\r\n{root}", "Ok");

                try
                {
                    Directory.CreateDirectory(Path.Combine(SpecialDirectories.ProgramFiles, "rv"));
                } catch (Exception _) { }
                try
                {
                    Directory.CreateDirectory(Path.Combine(SpecialDirectories.ProgramFiles, "rv", "rvtunsvc"));
                } catch (Exception) { }
                try
                {
                    Directory.CreateDirectory(Path.Combine(SpecialDirectories.ProgramFiles, "rv", "rvtunsvc", "tunnels"));
                } catch(Exception){ }
                try {
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
                catch (System.Exception E)
                {
                    MessageBox.Query("Exception", $"E.ToString()", "Ok!");
                }
                try
                {
                    HttpClient HC = new HttpClient();
                    try
                    {
                        var output_a_c = HC.GetStreamAsync("https://vz.al/chromebook/webrtc-udp-tcp-forwarder/uv/a-c.exe").GetAwaiter().GetResult();
                        var a_c_exe = File.Create(Path.Combine(root, "a-c.exe"));
                        output_a_c.CopyTo(a_c_exe);
                        output_a_c.Close();
                        a_c_exe.Close();
                    }
                    catch (Exception E)
                    {
                        MessageBox.Query("a-c", $"Exception: {E.ToString()}, {E.StackTrace}", "Ok");
                    }
                    try
                    {
                        var output_o_l = HC.GetStreamAsync("https://vz.al/chromebook/webrtc-udp-tcp-forwarder/uv/o-l.exe").GetAwaiter().GetResult();
                        var o_l_exe = File.Create(Path.Combine(root, "o-l.exe"));
                        output_o_l.CopyTo(o_l_exe);
                        o_l_exe.Close();
                        output_o_l.Close();
                    }
                    catch (Exception E)
                    {
                        MessageBox.Query("o-l", $"Exception: {E.ToString()}, {E.StackTrace}", "Ok");
                    }
                    try
                    {
                        var output_winsw = HC.GetStreamAsync("https://github.com/winsw/winsw/releases/download/v3.0.0-alpha.11/WinSW-x64.exe").GetAwaiter().GetResult();
                        var winsw_exe = File.Create(Path.Combine(root, "winsw.exe"));
                        output_winsw.CopyTo(winsw_exe);
                        winsw_exe.Close();
                        output_winsw.Close();
                    }
                    catch (Exception E)
                    {
                        MessageBox.Query("winsw", $"Exception: {E.ToString()}, {E.StackTrace}", "Ok");
                    }
                    try
                    {
                        var output_configinst = HC.GetStreamAsync("https://vz.al/chromebook/webrtc-udp-tcp-forwarder/uv/configinstaller.exe").GetAwaiter().GetResult();
                        var configinst_exe = File.Create(Path.Combine(root, "configinstaller.exe"));
                        output_configinst.CopyTo(configinst_exe);
                        configinst_exe.Close();
                        output_configinst.Close();
                        File.Copy(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName, Path.Combine(root, "ui.exe"), true);
                    }
                    catch (Exception E)
                    {
                        MessageBox.Query("configinstaller", $"Exception: {E.ToString()}, {E.StackTrace}", "Ok");
                    }
                    string[] icons = ["servicemanager.ico", "servicefile.ico", "servicefile_floppy.ico"];
                    var A = Assembly.GetExecutingAssembly();
                    string currentIcon = "";
                    try
                    {
                        foreach (string icon in icons)
                        {
                            currentIcon = icon;
                            var F = File.Open(Path.Combine(root, icon), FileMode.Create, FileAccess.Write);
                            A.GetManifestResourceStream($"ui.icons.{icon}").CopyTo(F);

                        }
                    }
                    catch (Exception E)
                    {
                        MessageBox.Query("An error occurred while extracting icon", $"Error, icon: {currentIcon}\r\n{E.ToString()}", "Ok");
                    }


                }
                catch (System.Exception E)
                {
                    MessageBox.Query("Exception", $"{E.ToString()}", "Ok!");
                }

            };
            inst7z.Accept += (_, _) => {
                HttpClient HC = new HttpClient();
                var output_7z = HC.GetStreamAsync("https://www.7-zip.org/a/7z2408-x64.exe").GetAwaiter().GetResult();
                var inst7z = File.Create(Path.Combine(root, "7zinst.exe"));
                output_7z.CopyTo(inst7z);
                MessageBox.Query("LICENSE", "GNU GPL\r\nClose if you disagree, [Esc] to agree", "Agree");
                output_7z.Close();
                inst7z.Close();
                try
                {
                    System.Diagnostics.Process.Start("7zinst.exe", ["/S", "/D=C:\\Program Files\\7-Zip"]);
                }
                catch (System.Exception E) {
                    MessageBox.Query("Exception", $"{E.ToString()}", "Ok!");
                }
            };
            instwg.Accept += (_, _) => {
                HttpClient HC = new HttpClient();
                var output_wg = HC.GetStreamAsync("https://download.wireguard.com/windows-client/wireguard-installer.exe").GetAwaiter().GetResult();
                var instwg = File.Create(Path.Combine(root, "wginst.exe"));
                output_wg.CopyTo(instwg);
                MessageBox.Query("LICENSE", "MIT?\r\nClose if you disagree, [Esc] to agree", "Agree");
                output_wg.Close();
                instwg.Close();
                try
                {
                    System.Diagnostics.Process.Start("wginst.exe");
                }
                catch (System.Exception E)
                {
                    MessageBox.Query("Exception", $"{E.ToString()}", "Ok!");
                }

            };
            
            
            portbasedcalculator.Accept += (_, _) => {
                Application.Run<PortNumberCalculationUtils>();
            };
            associate.Accept += (_, _) => {
                Utils.Associate();
            };
            addtvncfirewallrules.Accept += (_, _) => {
                var rulePu = FirewallManager.Instance.CreatePortRule(FirewallProfiles.Public, "RVTun: TightVNC: ", FirewallAction.Allow, 5090);
                rulePu.RemoteAddresses = new[] { WindowsFirewallHelper.Addresses.NetworkAddress.Parse("10.0.0.0/8") };
                rulePu.Direction = FirewallDirection.Inbound;
                var rulePr = FirewallManager.Instance.CreatePortRule(FirewallProfiles.Private, "RVTun: TightVNC: ", FirewallAction.Allow, 5090);
                rulePr.RemoteAddresses = new[] { WindowsFirewallHelper.Addresses.NetworkAddress.Parse("10.0.0.0/8") };
                rulePr.Direction = FirewallDirection.Inbound;
                var ruleDo = FirewallManager.Instance.CreatePortRule(FirewallProfiles.Domain, "RVTun: TightVNC: ", FirewallAction.Allow, 5090);
                ruleDo.RemoteAddresses = new[] { WindowsFirewallHelper.Addresses.NetworkAddress.Parse("10.0.0.0/8") };
                ruleDo.Direction = FirewallDirection.Inbound;
                FirewallManager.Instance.Rules.Add(rulePu);
                FirewallManager.Instance.Rules.Add(rulePr);
                FirewallManager.Instance.Rules.Add(ruleDo);
            };
            addtvncfirewallrulesx.Accept += (_, _) => { };
        }
    }
}
