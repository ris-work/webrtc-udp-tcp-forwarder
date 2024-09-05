
//------------------------------------------------------------------------------

//  <auto-generated>
//      This code was generated by:
//        TerminalGuiDesigner v2.0.0.0
//      You can make changes to this file and they will not be overwritten when saving.
//  </auto-generated>
// -----------------------------------------------------------------------------
namespace RV.WebRTCForwarders {
    using System.Text.RegularExpressions;
    using Terminal.Gui;
    
    
    public partial class PortNumberCalculationUtils {
        
        public PortNumberCalculationUtils() {
            InitializeComponent();
            portnumber.Text = "10010";
            confout.Enabled = false;

            calculatebutton.Accept += (_, _) => {
                MessageBox.Query(70, 24, "What's this?", "This makes a port number into a set of IP addresses; for internal use. " +
                    "Five digits, first two digits are 10, next one goes in the 10.x and the next two go in the y field of 10.x.y " +
                    "and the server is 1, client is 0 for z in 10.x.y.z.", "Ok");
                string addr = portnumber.Text;
                string[] a = Regex.Split(addr, String.Empty);
                int Addr_8 = int.Parse(a[0] + a[1]);
                int Addr_8_16 = int.Parse(a[2]);
                int Addr_16_24 = int.Parse(a[3]+a[4]);
                int Addr_24_32 = role.SelectedItem == 0 ? 1 : 2;
                int Addr_24_32_peer = Addr_24_32 == 1 ? 2 : 1;
                string Addresses = $"{Addr_8}.{Addr_8_16}.{Addr_16_24}.{Addr_24_32}/24";
                string PeerAllowedIPs = $"{Addr_8}.{Addr_8_16}.{Addr_16_24}.{Addr_24_32}/32";
                string configuration;
                configuration = $"Address = {Addresses}\r\n";
                configuration += $"PrivateKey = [Privkey]\r\n";
                if(role.SelectedItem == 0)
                {
                    configuration += $"ListenPort = {portnumber.Text}\r\n";
                }
                configuration += $"\r\n\r\n[Peer]\r\n";
                if (role.SelectedItem == 1)
                {
                    configuration += $"Endpoint = 127.0.0.1:{portnumber.Text}\r\n";
                }
                configuration += $"AllowedIPs = {PeerAllowedIPs}\r\n";
                configuration += $"PublicKey = [Peer_pubkey]";
                confout.Text = configuration;
                };
        }
    }
}
