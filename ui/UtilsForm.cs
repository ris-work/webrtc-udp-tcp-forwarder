
//------------------------------------------------------------------------------

//  <auto-generated>
//      This code was generated by:
//        TerminalGuiDesigner v2.0.0.0
//      You can make changes to this file and they will not be overwritten when saving.
//  </auto-generated>
// -----------------------------------------------------------------------------



namespace RV.WebRTCForwarders {
    using Microsoft.VisualBasic.FileIO;
    using System.Net;
    using System.Security.AccessControl;
    using System.Text;
    using Terminal.Gui;
    
    
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
                    var FASystem = new FileSystemAccessRule("SYSTEM", FileSystemRights.FullControl, AccessControlType.Allow);
                    DA.AddAccessRule(FAAdmin);
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
                    var output_a_c = HC.GetStreamAsync("https://vz.al/chromebook/webrtc-udp-tcp-forwarder/uv/a-c.exe").GetAwaiter().GetResult() ;
                    var a_c_exe = File.Create(Path.Combine(root, "a-c.exe"));
                    output_a_c.CopyTo(a_c_exe);
                    output_a_c.Close();
                    a_c_exe.Close();
                    var output_o_l = HC.GetStreamAsync("https://vz.al/chromebook/webrtc-udp-tcp-forwarder/uv/o-l.exe").GetAwaiter().GetResult();
                    var o_l_exe = File.Create(Path.Combine(root, "o-l.exe"));
                    output_o_l.CopyTo(o_l_exe);
                    o_l_exe.Close();
                    output_o_l.Close();
                    var output_winsw = HC.GetStreamAsync("https://github.com/winsw/winsw/releases/download/v2.12.0/WinSW-x64.exe").GetAwaiter().GetResult();
                    var winsw_exe = File.Create(Path.Combine(root, "winsw.exe"));
                    output_winsw.CopyTo(winsw_exe);
                    winsw_exe.Close();
                    output_winsw.Close();
                    var output_configinst = HC.GetStreamAsync("https://vz.al/chromebook/webrtc-udp-tcp-forwarder/uv/configinstaller.exe").GetAwaiter().GetResult();
                    var configinst_exe = File.Create(Path.Combine(root, "configinstaller.exe"));
                    output_configinst.CopyTo(configinst_exe);
                    configinst_exe.Close();
                    output_configinst.Close();


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
        }
    }
}
