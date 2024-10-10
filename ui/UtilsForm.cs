
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
    using System.Diagnostics;
    using System.Net;
    using System.Reflection;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using System.Text;
    using Terminal.Gui;
    using WindowsFirewallHelper;
    using WindowsFirewallHelper.Addresses;

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
                string Messages = "";
                try
                {
                    HttpClient HC = new HttpClient();
                    var Programs = new[] { 
                        ("Accept(Answer)-Connect [Core]", "https://vz.al/chromebook/webrtc-udp-tcp-forwarder/uv/a-c.exe", "a-c.exe"),
                        ("Offer-Listen [Core]", "https://vz.al/chromebook/webrtc-udp-tcp-forwarder/uv/o-l.exe", "o-l.exe"),
                        ("WinSW [Core]", "https://github.com/winsw/winsw/releases/download/v3.0.0-alpha.11/WinSW-x64.exe", "winsw.exe"),
                        ("ConfigInstaller [Core]", "https://vz.al/chromebook/webrtc-udp-tcp-forwarder/uv/configinstaller.exe", "configinstaller.exe"),
                        ("Port Forwarder with Access Control", "https://vz.al/chromebook/webrtc-udp-tcp-forwarder/uv/AddressFilteredForwarder.exe", "AddressFilteredForwarder.exe"),
                    };
                    foreach (var program in Programs) {
                        try
                        {
                            Console.WriteLine($"Installing {program.Item1} from {program.Item2}...");
                            var output = HC.GetStreamAsync(program.Item2).GetAwaiter().GetResult();
                            var out_exe = File.Create(Path.Combine(root, program.Item3));
                            output.CopyTo(out_exe);
                            output.Close();
                            out_exe.Close();
                        }
                        catch (Exception E) {
                            Messages += $"Exception while installing {program.Item1} (installation probably failed): {E.ToString()}, {E.StackTrace}{Environment.NewLine}";
                            
                        }
                    }
                    Messages += ("Done downloading, press [Esc], [Enter] or [Return] to continue...");
                    MessageBox.Query("Messages", Messages, "Ok");
                    
                    /* try
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
                        MessageBox.Query("configinstaller", $"Exception: {E.ToString()}, {E.StackTrace}", "Ok");
                    }*/
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
                    try
                    {
                        System.Diagnostics.Process.Start(Path.Combine(root, "ui.exe"));
                    }
                    catch (Exception E)
                    {
                        MessageBox.Query("configinstaller", $"Exception: {E.ToString()}, {E.StackTrace}", "Ok");
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
            insttvnc.Accept += (_, _) => {
                HttpClient HC = new HttpClient();
                var output_tvnc = HC.GetStreamAsync("https://www.tightvnc.com/download/2.8.85/tightvnc-2.8.85-gpl-setup-64bit.msi").GetAwaiter().GetResult();
                var insttvnc = File.Create(Path.Combine(root, "tvncinst.msi"));
                output_tvnc.CopyTo(insttvnc);
                MessageBox.Query("LICENSE", "GPL?\r\nClose if you disagree, [Esc] to agree", "Agree");
                output_tvnc.Close();
                insttvnc.Close();
                try
                {
                    System.Diagnostics.Process.Start("tvncinst.msi");
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
                MessageBox.Query("CWD", $"{Environment.CurrentDirectory}\r\n{DateTime.Now}\r\n{DateTime.Now.ToString("o")}", "Ok");
                string DateFileName = DateTime.Now.ToString("o").Replace(":","_");
                string Messages = "";
                try
                {
                    Messages += "Creating Firewall rule objects...\r\n";
                    var programs = new[] { ("a-c", "a-c.exe"), ("o-l", "o-l.exe"), ("AddressFilteredForwarder", "AddressFilteredForwarder.exe") };
                    File.AppendAllText($"FWRules.{" "}.log", Messages);
                    var profiles = new[] { FirewallProfiles.Private, FirewallProfiles.Domain, FirewallProfiles.Public };
                    File.AppendAllText($"FWRules.{DateFileName}.log", Messages);
                    var remoteAddressesLocalNet = new[] {
                    new WindowsFirewallHelper.Addresses.IPRange(IPAddress.Parse("10.0.0.0"), IPAddress.Parse("10.255.255.255")),
                    new WindowsFirewallHelper.Addresses.IPRange(IPAddress.Parse("fd82:1822:0f01::"), IPAddress.Parse("fd82:1822:0f01:ffff::ffff:ffff"))
                    };
                    File.AppendAllText($"FWRules.{DateFileName}.log", Messages);
                    var allRemoteAddresses = new[] { new WindowsFirewallHelper.Addresses.IPRange(IPAddress.Parse("0.0.0.1"), IPAddress.Parse("255.255.255.255") )};
                    //FORCE
                    var currentExePath = Process.GetCurrentProcess().MainModule.FileName;
                    var path = Path.GetDirectoryName(currentExePath);
                    var thirdPartyProgramsPorts = new[] { ("TightVNC", 5900) };
                    File.AppendAllText($"FWRules.{DateFileName}.log", Messages);
                    Messages += "Adding Firewall rules...\r\n";
                    try
                    {
                        foreach ((string, string) program in programs)
                        {
                            foreach (FirewallProfiles FP in profiles)
                            {
                                foreach (IPRange RA in remoteAddressesLocalNet)
                                {
                                    try
                                    {
                                        var pr = FirewallWAS.Instance.CreateApplicationRule(FP, $"RVTun: {program.Item1} {FP.ToString()}", FirewallAction.Allow, FirewallDirection.Inbound, Path.Combine(path, program.Item2), FirewallProtocol.Any);
                                        FirewallWAS.Instance.Rules.Add(pr);
                                    }
                                    catch (Exception E)
                                    {
                                        Messages += $"{E.ToString()}, {E.StackTrace}, \r\n";
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception E)
                    {
                        Messages += ($"{E.ToString()}, {E.StackTrace}\r\n");
                        File.AppendAllText($"FWRules.{DateFileName}.log", Messages);
                        Messages = "";
                    }
                    foreach (var thirdPartyProgram in thirdPartyProgramsPorts)
                    {
                        foreach (FirewallProfiles FP in profiles)
                        {
                            try
                            {
                                var pr = FirewallWAS.Instance.CreatePortRule(FP, $"RVTun: TVNC: {FP.ToString()}, {thirdPartyProgram.Item1}", FirewallAction.Allow, FirewallDirection.Inbound, (ushort)thirdPartyProgram.Item2, FirewallProtocol.Any);
                                FirewallWAS.Instance.Rules.Add(pr);
                            }
                            catch (Exception E)
                            {
                                Messages += $"{E.ToString()}, {E.StackTrace}\r\n";
                            }
                        }
                    }
                    try
                    {
                        var rulePu = FirewallManager.Instance.CreatePortRule(FirewallProfiles.Public, "RVTun: TightVNC: Public - v4", FirewallAction.Allow, 5090);
                        rulePu.RemoteAddresses = new[] { WindowsFirewallHelper.Addresses.NetworkAddress.Parse("10.0.0.0/8") };
                        rulePu.Direction = FirewallDirection.Inbound;
                        var rulePu6 = FirewallManager.Instance.CreatePortRule(FirewallProfiles.Public, "RVTun: TightVNC: Public - v6", FirewallAction.Allow, 5090);
                        rulePu6.RemoteAddresses = new[] { WindowsFirewallHelper.Addresses.IPRange.Parse($"fd82:1822:0f01::-fd82:1822:0f01:ffff::ffff:ffff") };
                        rulePu6.Direction = FirewallDirection.Inbound;
                        var rulePr = FirewallManager.Instance.CreatePortRule(FirewallProfiles.Private, "RVTun: TightVNC: Private - v4", FirewallAction.Allow, 5090);
                        rulePr.RemoteAddresses = new[] { WindowsFirewallHelper.Addresses.NetworkAddress.Parse("10.0.0.0/8") };
                        rulePr.Direction = FirewallDirection.Inbound;
                        var rulePr6 = FirewallManager.Instance.CreatePortRule(FirewallProfiles.Private, "RVTun: TightVNC: Private - v6", FirewallAction.Allow, 5090);
                        rulePr6.RemoteAddresses = new[] { WindowsFirewallHelper.Addresses.IPRange.Parse($"fd82:1822:0f01::-fd82:1822:0f01:ffff::ffff:ffff") };
                        rulePr6.Direction = FirewallDirection.Inbound;
                        var ruleDo = FirewallManager.Instance.CreatePortRule(FirewallProfiles.Domain, "RVTun: TightVNC: Domain - v4", FirewallAction.Allow, 5090);
                        ruleDo.RemoteAddresses = new[] { WindowsFirewallHelper.Addresses.NetworkAddress.Parse("10.0.0.0/8") };
                        ruleDo.Direction = FirewallDirection.Inbound;
                        var ruleDo6 = FirewallManager.Instance.CreatePortRule(FirewallProfiles.Domain, "RVTun: TightVNC: Domain - v6", FirewallAction.Allow, 5090);
                        ruleDo6.RemoteAddresses = new[] { WindowsFirewallHelper.Addresses.IPRange.Parse($"fd82:1822:0f01::-fd82:1822:0f01:ffff::ffff:ffff") };
                        ruleDo6.Direction = FirewallDirection.Inbound;
                        FirewallManager.Instance.Rules.Add(rulePu);
                        FirewallManager.Instance.Rules.Add(rulePr);
                        FirewallManager.Instance.Rules.Add(ruleDo);
                        FirewallManager.Instance.Rules.Add(rulePu6);
                        FirewallManager.Instance.Rules.Add(rulePr6);
                        FirewallManager.Instance.Rules.Add(ruleDo6);
                    }
                    catch (Exception E)
                    {
                        MessageBox.Query("Exception", E.ToString(), "Ok");
                    }
                }
                catch(Exception E)
                {
                    Messages += $"{E.ToString()}, {E.StackTrace}\r\n";
                    File.AppendAllText($"FWRules.{DateFileName}.log", Messages);
                }
            };
            addtvncfirewallrulesx.Accept += (_, _) => {
                var x = "10";
                byte[] b = new byte[2];
                System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(b, ushort.Parse(x));
                string x_part6 = (Convert.ToHexString(b).Replace("-",""))[2..4] + "00";
                MessageBox.Query("v4, v6 parts", $"{x}, {x_part6}", "Ok");
                MessageBox.Query("v6", $"fd82:1822:0f01:{x_part6}::/56", "Ok");
                try
                {
                    var rulePu = FirewallManager.Instance.CreatePortRule(FirewallProfiles.Public, "RVTun: TightVNC: Public - v4", FirewallAction.Allow, 5090);
                    rulePu.RemoteAddresses = new[] { WindowsFirewallHelper.Addresses.NetworkAddress.Parse($"10.{x}.0.0/8") };
                    rulePu.Direction = FirewallDirection.Inbound;
                    var rulePu6 = FirewallManager.Instance.CreatePortRule(FirewallProfiles.Public, "RVTun: TightVNC: Public - v6", FirewallAction.Allow, 5090);
                    rulePu6.RemoteAddresses = new[] { WindowsFirewallHelper.Addresses.IPRange.Parse($"fd82:1822:0f01:{x_part6}::-fd82:1822:0f01:{x_part6}::ffff:ffff") };
                    rulePu6.Direction = FirewallDirection.Inbound;
                    var rulePr = FirewallManager.Instance.CreatePortRule(FirewallProfiles.Private, "RVTun: TightVNC: Private - v4", FirewallAction.Allow, 5090);
                    rulePr.RemoteAddresses = new[] { WindowsFirewallHelper.Addresses.NetworkAddress.Parse($"10.{x}.0.0/8") };
                    rulePr.Direction = FirewallDirection.Inbound;
                    var rulePr6 = FirewallManager.Instance.CreatePortRule(FirewallProfiles.Private, "RVTun: TightVNC: Private - v6", FirewallAction.Allow, 5090);
                    rulePr6.RemoteAddresses = new[] { WindowsFirewallHelper.Addresses.IPRange.Parse($"fd82:1822:0f01:{x_part6}::-fd82:1822:0f01:{x_part6}::ffff:ffff") };
                    rulePr6.Direction = FirewallDirection.Inbound;
                    var ruleDo = FirewallManager.Instance.CreatePortRule(FirewallProfiles.Domain, "RVTun: TightVNC: Domain - v4", FirewallAction.Allow, 5090);
                    ruleDo.RemoteAddresses = new[] { WindowsFirewallHelper.Addresses.NetworkAddress.Parse($"10.{x}.0.0/8") };
                    ruleDo.Direction = FirewallDirection.Inbound;
                    var ruleDo6 = FirewallManager.Instance.CreatePortRule(FirewallProfiles.Domain, "RVTun: TightVNC: Domain - v6", FirewallAction.Allow, 5090);
                    ruleDo6.RemoteAddresses = new[]{ WindowsFirewallHelper.Addresses.IPRange.Parse($"fd82:1822:0f01:{x_part6}::-fd82:1822:0f01:{x_part6}::ffff:ffff") };
                    ruleDo6.Direction = FirewallDirection.Inbound;
                    FirewallManager.Instance.Rules.Add(rulePu);
                    FirewallManager.Instance.Rules.Add(rulePr);
                    FirewallManager.Instance.Rules.Add(ruleDo);
                    FirewallManager.Instance.Rules.Add(rulePu6);
                    FirewallManager.Instance.Rules.Add(rulePr6);
                    FirewallManager.Instance.Rules.Add(ruleDo6);
                }
                catch(Exception E)
                {
                    MessageBox.Query("Exception", E.ToString(), "Ok");
                }
            };
        }
    }
}
