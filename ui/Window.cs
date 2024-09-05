
//------------------------------------------------------------------------------

//  <auto-generated>
//      This code was generated by:
//        TerminalGuiDesigner v2.0.0.0
//      You can make changes to this file and they will not be overwritten when saving.
//  </auto-generated>
// -----------------------------------------------------------------------------
namespace RV.WebRTCForwarders {
    using Terminal.Gui;
    using Tomlyn.Model;
    using Tomlyn;
    using System.Security.AccessControl;
    using System.Data;
    using System.Net.Sockets;

    public partial class Window {
        
        public Window() {
            InitializeComponent();
            Title = $"Configuration Editor ({Application.QuitKey} to Quit)";
            string TomlFileContents = System.IO.File.ReadAllText(StartConfig.Filename);
            var model = ((TomlTable)Toml.ToModel(TomlFileContents)).ToDictionary<string, object>();
            filename.Text = StartConfig.Filename;
            localtype.Enabled = false;
            List<IceServers> iceServers = new List<IceServers>();
            iceServers = ((TomlArray)model["ICEServers"]).Select( x => { 
                var a = ((TomlTable)x).ToDictionary();
                return new IceServers
                {
                    URLs = ((TomlArray)a["URLs"]).Select(x => (string)x).ToArray(),
                    Username = (string)a.GetValueOrDefault("Username", ""),
                    Credential = (string)a.GetValueOrDefault("Credential", ""),
                };
            } ).ToList();
            DataTable T = new DataTable();
            T.Columns.Add("URLs");
            T.Columns.Add("Username");
            T.Columns.Add("Credential");

            if((string)model.GetValueOrDefault("WebRTCMode", "Accept") == "Accept")
            {
                webrtcmode.SelectedItem = 0;
            }
            else
            {
                webrtcmode.SelectedItem = 1;
            }
            addrlocal.Text = (string)model.GetValueOrDefault("Address", "127.0.0.1");
            portlocal.Text = (string)model.GetValueOrDefault("Port", "10010");
            tunnelname.Text = StartConfig.Filename.Split('.')[0];
            peerpsk.Text = (string)model.GetValueOrDefault("PeerPSK", "(secret)");
            publishauthuser.Text = (string)model.GetValueOrDefault("PublishAuthUser", "Will be sent in plain");
            publishauthpass.Text = (string)model.GetValueOrDefault("PublishAuthPass", "text, will be matched");
            autogenerate.MouseClick += (_, _) =>
            {
                string URLBase = "wss://vz.al/anonwsmul";
                string URLBaseWithTunnelName = URLBase + "/" + tunnelname.Text;
                string OfferAcceptSuffix = webrtcmode.SelectedItem == 0 ? "/wsa" : "/wso";
                string FullURL = URLBaseWithTunnelName + OfferAcceptSuffix;
                publishendpoint.Text = FullURL;
            };
            peerauthtype.Enabled = false;


            foreach (var i in iceServers) {
                T.Rows.Add(String.Join(",", i.URLs), i.Username, i.Credential);
            }
            icecandidates.Table = new DataTableSource(T);
            addicecandidate.Accept += (e, a) => {
                var result = Application.Run<IceCandidateEditor>();
                if (!result.Cancelled)
                {
                    T.Rows.Add(result.URLs, result.Username, result.Credential);
                }
            };
            removeicecandidate.Accept += (_, _) => {
                T.Rows.RemoveAt((icecandidates.SelectedRow));
            };
            editicecandidate.Accept += (_, _) =>
            {
                var selectedValue = T.Rows[icecandidates.SelectedRow];
                var editorDialog = new IceCandidateEditor(selectedValue);
                Application.Run(editorDialog);
                if (!editorDialog.Canceled)
                {
                    (T.Rows[icecandidates.SelectedRow]).BeginEdit();
                    T.Rows[icecandidates.SelectedRow][0] = editorDialog.URLs;
                    T.Rows[icecandidates.SelectedRow][1] = editorDialog.Username;
                    T.Rows[icecandidates.SelectedRow][2] = editorDialog.Credential;
                    (T.Rows[icecandidates.SelectedRow]).EndEdit();
                }
            };
            launchutils.Accept += (_, _) => {
                var portNumberCalculatorWindow = new PortNumberCalculationUtils();
                Application.Run(portNumberCalculatorWindow);
            };
        }
    }
}
