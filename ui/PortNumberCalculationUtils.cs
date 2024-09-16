
//------------------------------------------------------------------------------

//  <auto-generated>
//      This code was generated by:
//        TerminalGuiDesigner v2.0.0.0
//      You can make changes to this file and they will not be overwritten when saving.
//  </auto-generated>
// -----------------------------------------------------------------------------
namespace RV.WebRTCForwarders {
    using System.Diagnostics;
    using System.Text.RegularExpressions;
    using Terminal.Gui;
    using Wiry.Base32;
    using System.Security.Cryptography;
    using System.Buffers.Text;
    using ICSharpCode.SharpZipLib;
    using ICSharpCode.SharpZipLib.Zip;
    using System.Text;
    using System.Xml;
    using Tomlyn.Model;
    using Tomlyn;    

    public partial class PortNumberCalculationUtils {
        public void AddAddressFilteredPortForwarderConfiguration(ZipOutputStream ZF, string[] OurAddress, string[] AllowedSubnet, int TunnelNumber)
        {
            ZipEntry ZE_PS_PF = new ZipEntry("aff.ps1");
            ZE_PS_PF.AESKeySize = 256;
            ZF.PutNextEntry(ZE_PS_PF);

            string runCommandAff = "..\\..\\AddressFilteredForwarder.exe";
            string powerShellScriptOursAff = "do {\r\n" +
            $"{runCommandAff} .\\aff.toml\r\n" +
            $"Start-Sleep -Seconds 2\r\n" +
            "}\r\n" +
            "until ($false)";
            ZF.Write(Encoding.UTF8.GetBytes(powerShellScriptOursAff));
            ZF.Flush();
            ZF.CloseEntry();



            ZipEntry ZE_XML_PFF = new ZipEntry("aff.xml");
            ZE_XML_PFF.AESKeySize = 256;
            ZF.PutNextEntry(ZE_XML_PFF);
            var XS = new XmlWriterSettings()
            {
                Indent = true,
                NewLineChars = "\r\n"
            };

            var XW = XmlWriter.Create(ZF);
            XW.WriteStartElement("service");
            XW.WriteStartElement("id");
            XW.WriteString($"RV-TunnelService-AFF-{TunnelNumber}");
            XW.WriteEndElement();
            XW.WriteStartElement("name");
            XW.WriteString($"RV-TunnelService-AFF-{TunnelNumber}");
            XW.WriteEndElement();
            XW.WriteStartElement("executable");
            XW.WriteString($"powershell");
            XW.WriteEndElement();
            XW.WriteStartElement("arguments");
            XW.WriteString($"-ExecutionPolicy Bypass .\\aff.ps1");
            XW.WriteEndElement();
            //XW.WriteStartElement("workingdirectory");
            //XW.WriteString(Path.Combine("", "tunnels", portInt.ToString()));
            //XW.WriteEndElement();
            XW.WriteStartElement("description");
            XW.WriteString($"Secure WebRTC based end-to-end tunnel port: {TunnelNumber}.");
            XW.WriteEndElement();

            XW.WriteStartElement("log");
            XW.WriteStartAttribute("mode");
            XW.WriteString("roll");
            XW.WriteEndAttribute();
            XW.WriteEndElement();
            XW.WriteEndElement();
            XW.Flush();
            ZF.Flush();

            ZF.CloseEntry();

            Utils.AddressFilteredPortForwarderConfigOut PFFConf = new Utils.AddressFilteredPortForwarderConfigOut()
            {
                AllowedSources = new TomlArray() { AllowedSubnet[0], AllowedSubnet[1] },
                DestinationAddress = "127.0.0.1",
                DestinationPort = 5900,
                ListenAddresses = new TomlArray() { OurAddress[0], OurAddress[1] },
                ListenPort = 6000,
            };
            TomlTable PFFTomlModel = PFFConf.ToTomlTable();
            ZipEntry ZE_TOML_PFF = new ZipEntry("aff.toml");
            ZE_TOML_PFF.AESKeySize = 256;
            ZF.PutNextEntry(ZE_TOML_PFF);
            ZF.Write(Encoding.UTF8.GetBytes(Toml.FromModel(PFFTomlModel)));
            ZF.Flush();
            ZF.CloseEntry();
            ZF.Flush();

        }
        
        public PortNumberCalculationUtils() {
            InitializeComponent();
            portnumber.Text = "10010";
            confout.Enabled = false;
            whatisthis.Enabled = false;
            confoutTheirs.Enabled = false;
            psk.Enabled = false;
            calculatebutton.Accept += (_, _) => {
                MessageBox.Query(70, 24, "What's this?", "This makes a port number into a set of IP addresses; for internal use. " +
                    "Five digits, first three digits go in the 10.x and the next two go in the y field of 10.x.y " +
                    "and the server is 1, client is 2 for z in 10.x.y.z.", "Ok");
                string addr = portnumber.Text;
                string[] a = Regex.Split(addr, String.Empty);
                int Addr_8 = 10;
                int Addr_8_16 = int.Parse(a[0]+a[1]+a[2]);
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
                confout.SelectAll();
                confout.Copy();
                };
            genkeysbutton.Accept += (_, _) => {
                try
                {
                    Console.Title = $"Current: {portnumber.Text}";
                    var PrivO = new ProcessStartInfo()
                    {
                        FileName = "wg",
                        Arguments = "genkey",
                        RedirectStandardOutput = true,
                    };
                    var PSIPSK = new ProcessStartInfo()
                    {
                        FileName = "wg",
                        Arguments = "genpsk",
                        RedirectStandardOutput = true,
                    };
                    var ProcessPrivO = Process.Start(PrivO);
                    string privKeyO = ProcessPrivO.StandardOutput.ReadToEnd();
                    privKeyOurs.Text = privKeyO;
                    var PSIPSKo = Process.Start(PSIPSK);
                    string PSKs = PSIPSKo.StandardOutput.ReadToEnd();
                    PSKs = PSKs.Replace("\n", "").Replace("\r", "");
                    psk.Text = PSKs;
                    var PubO = new ProcessStartInfo()
                    {
                        FileName = "wg",
                        Arguments = "pubkey",
                        RedirectStandardOutput = true,
                        RedirectStandardInput = true,
                    };
                    var ProcessPubO = Process.Start(PubO);
                    ProcessPubO.StandardInput.Write(privKeyO);
                    ProcessPubO.StandardInput.Flush();
                    ProcessPubO.StandardInput.Close();
                    pubKeyOurs.Text = ProcessPubO.StandardOutput.ReadToEnd();
                    var ProcessPrivT = Process.Start(PrivO);
                    string privKeyT = ProcessPrivT.StandardOutput.ReadToEnd();
                    privKeyTheirs.Text = privKeyT;
                    var ProcessPubT = Process.Start(PubO);
                    ProcessPubT.StandardInput.Write(privKeyT);
                    ProcessPubT.StandardInput.Flush();
                    ProcessPubT.StandardInput.Close();
                    pubKeyTheirs.Text = ProcessPubT.StandardOutput.ReadToEnd();

                    /* Config */
                    string addr = portnumber.Text;
                    string[] a = Regex.Split(addr, String.Empty);
                    int Addr_8 = int.Parse("10");
                    int Addr_8_16 = int.Parse(a[1]+a[2]+a[3]);
                    int Addr_16_24 = int.Parse(a[4] + a[5]);
                    int Addr_24_32 = role.SelectedItem == 0 ? 1 : 2;
                    int Addr_24_32_peer = Addr_24_32 == 1 ? 2 : 1;
                    string Addresses = $"{Addr_8}.{Addr_8_16}.{Addr_16_24}.{Addr_24_32}/24";
                    string PeerAllowedIPs = $"{Addr_8}.{Addr_8_16}.{Addr_16_24}.{Addr_24_32_peer}/32";
                    string configuration;
                    configuration = $"Address = {Addresses}\r\n";
                    configuration += $"PrivateKey =  {privKeyO}\r\n";
                    if (role.SelectedItem == 0)
                    {
                        configuration += $"ListenPort = {portnumber.Text}\r\n";
                    }
                    configuration += $"\r\n\r\n[Peer]\r\n";
                    if (role.SelectedItem == 1)
                    {
                        configuration += $"Endpoint = 127.0.0.1:{portnumber.Text}\r\n";
                    }
                    configuration += $"AllowedIPs = {PeerAllowedIPs}\r\n";
                    configuration += $"PublicKey = {pubKeyTheirs.Text}";
                    configuration += $"PresharedKey = {PSKs}\r\n";
                    confout.Text = configuration;
                    confout.SelectAll();
                    confout.Copy();
                    byte[] random8bytes = new byte[16];
                    byte[] peerPSK = new byte[40];
                    byte[] randomUsernameBytes = new byte[20];
                    byte[] randomPasswordBytes = new byte[20];
                    byte[] randomSessionNameBytes = new byte[40];
                    var RNG = RandomNumberGenerator.Create();
                    RNG.GetBytes(random8bytes);
                    RNG.GetBytes(peerPSK);
                    RNG.GetBytes(randomUsernameBytes);
                    RNG.GetBytes(randomPasswordBytes);
                    RNG.GetBytes(randomSessionNameBytes);
                    string random128bits = Wiry.Base32.Base32Encoding.Standard.GetString(random8bytes)[0..26];
                    
                    string random128bitsHumanFriendly = Utils.MakeItLookLikeACdKey(random128bits);
                    MessageBox.Query("Keep this safe!", $"You won't see this again;\r\nWrite it down: \r\n{random128bitsHumanFriendly}", "Done!");
                    MessageBox.Query("Decoded debug", $"Decoded debug: \r\n{
                        Convert.ToBase64String(
                            Wiry.Base32.Base32Encoding.Standard.ToBytes(
                                Utils.MakeItNormalBase32(random128bitsHumanFriendly)
                            )
                        )
                        }", "Ok");
                    MessageBox.Query("Warning", $"This will overwrite: {portnumber.Text}.tun.theirs.zip with an encrypted ZIP.\r\n" +
                        $"This will overwrite: {portnumber.Text}.tun.ours.zip with an encrypted ZIP.\r\n" +
                        $"Close the program immediately if you don't want this.", "Ok, I understand");

                    /* Other side configuration */
                    string addrT = portnumber.Text;
                    portnumber.Text = (int.Parse(portnumber.Text)).ToString();
                    string[] aT = Regex.Split(addr, String.Empty);
                    int Addr_8_T = int.Parse("10");
                    int Addr_8_16_T = int.Parse(a[1] + a[2] + a[3]);
                    int Addr_16_24_T = int.Parse(a[4] + a[5]);
                    int our_suffix = role.SelectedItem == 0 ? 1 : 2;
                    int their_suffix = Addr_24_32 == 1 ? 2 : 1;
                    string AddressesT = $"{Addr_8_T}.{Addr_8_16_T}.{Addr_16_24_T}.{their_suffix}/24";
                    string PeerAllowedIPsT = $"{Addr_8_T}.{Addr_8_16_T}.{Addr_16_24_T}.{our_suffix}/32";
                    string AddressesO = $"{Addr_8_T}.{Addr_8_16_T}.{Addr_16_24_T}.{our_suffix}/24";
                    string AddressS = $"{Addr_8_T}.{Addr_8_16_T}.{Addr_16_24_T}.1";
                    string AddressSSubnet = $"{Addr_8_T}.{Addr_8_16_T}.{Addr_16_24_T}.0/24";
                   
                    string PeerAllowedIPsO = $"{Addr_8_T}.{Addr_8_16_T}.{Addr_16_24_T}.{their_suffix}/32";
                    string configurationT;
                    int portInt = int.Parse(portnumber.Text);
                    byte[] portHex_BE = new byte[2];
                    System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(portHex_BE, (ushort)portInt);
                    string portHex = BitConverter.ToString((portHex_BE)).Replace("-","");
                    string addrT6 = $"fd82:1822:0f01:{portHex}::{our_suffix}/64";
                    string addrT6_allowed = $"fd82:1822:0f01:{portHex}::{their_suffix}/128";
                    string addrT6_theirs = $"fd82:1822:0f01:{portHex}::{their_suffix}/64";
                    string addrT6_theirs_allowed = $"fd82:1822:0f01:{portHex}::{our_suffix}/128";
                    string AddressS6 = $"fd82:1822:0f01:{portHex}::1";
                    string AddressS6Subnet = $"fd82:1822:0f01:{portHex}::/64";
                    configurationT = "[Interface]\r\n";
                    configurationT += $"Address = {AddressesT}, {addrT6_theirs}\r\n";
                    configurationT += $"PrivateKey =  {privKeyTheirs.Text}\r\n";
                    
                    if (role.SelectedItem == 1)
                    {
                        configurationT += $"ListenPort = {portnumber.Text}\r\n";
                    }
                    configurationT += $"[Peer]\r\n";
                    if (role.SelectedItem == 0)
                    {
                        configurationT += $"Endpoint = 127.0.0.1:{portnumber.Text}\r\n";
                        configurationT += "PersistentKeepAlive = 5\r\n";
                    }
                    configurationT += $"AllowedIPs = {PeerAllowedIPsT}, {addrT6_theirs_allowed}\r\n";
                    configurationT += $"PublicKey = {pubKeyOurs.Text}";
                    
                    
                    string configurationO;
                    configurationO = "[Interface]\r\n";
                    configurationO += $"Address = {AddressesO}, {addrT6}\r\n";
                    configurationO += $"PrivateKey =  {privKeyOurs.Text}\r\n";
                    if (role.SelectedItem == 0)
                    {
                        configurationO += $"ListenPort = {portnumber.Text}\r\n";
                    }
                    configurationO += $"[Peer]\r\n";
                    if (role.SelectedItem == 1)
                    {
                        configurationO += $"Endpoint = 127.0.0.1:{portnumber.Text}\r\n";
                    }
                    configurationO += $"AllowedIPs = {PeerAllowedIPsO}, {addrT6_allowed}\r\n";
                    configurationO += $"PublicKey = {pubKeyTheirs.Text}";
                    configurationO += $"PresharedKey = {PSKs}\r\n";
                    configurationT += $"PresharedKey = {PSKs}\r\n";
                    confout.Text = "#Ours:\r\n" + configurationO;
                    confout.SelectAll();
                    confoutTheirs.Text = "#Theirs: \r\n" + configurationT;
                    confoutTheirs.SelectAll();

                    string randomPeerPSK = Wiry.Base32.Base32Encoding.Standard.GetString(peerPSK);
                    string randomUsername = Wiry.Base32.Base32Encoding.Standard.GetString(randomUsernameBytes);
                    string randomPassword = Wiry.Base32.Base32Encoding.Standard.GetString(randomPasswordBytes);
                    string randomSessionName = Wiry.Base32.Base32Encoding.Standard.GetString(randomSessionNameBytes);

                    /* Generate WebRTC Forwarder TOML configuration */
                    var OffererToml = Toml.FromModel((new Utils.ForwarderConfigOut()
                    {
                        Address = "127.0.0.1",
                        PublishAuthUser = randomUsername,
                        PublishAuthPass = randomPassword,
                        PeerPSK = randomPeerPSK,
                        PublishEndpoint = $"wss://vz.al/anonwsmul/{randomSessionName}/wso",
                        Port = portnumber.Text,
                        PublishAuthType = "Basic",
                        Type = "UDP",
                        WebRTCMode = "Offer",
                    }).ToTomlTable());
                    var AnswererToml = Toml.FromModel((new Utils.ForwarderConfigOut()
                    {
                        Address = "127.0.0.1",
                        PublishAuthUser = randomUsername,
                        PublishAuthPass = randomPassword,
                        PeerPSK = randomPeerPSK,
                        PublishEndpoint = $"wss://vz.al/anonwsmul/{randomSessionName}/wsa",
                        Port = portnumber.Text,
                        PublishAuthType = "Basic",
                        Type = "UDP",
                        WebRTCMode = "Accept",
                    }).ToTomlTable());
                    var ourForwarderToml = role.SelectedItem == 0 ? AnswererToml : OffererToml;
                    var theirForwarderToml = role.SelectedItem == 1 ? AnswererToml : OffererToml;

                    /* Create the ZIP file */

                    var ZOT = new ZipOutputStream(File.Create($"{portnumber.Text}.tun.otherside.zip.rvtunnelconfiguration"));
                    ZOT.Password = random128bitsHumanFriendly;

                    ZipEntry ZE_FW_T = new ZipEntry("tunnel.toml");
                    ZE_FW_T.AESKeySize = 256;

                    ZOT.PutNextEntry(ZE_FW_T);

                    ZOT.Write(Encoding.UTF8.GetBytes(theirForwarderToml));
                    ZOT.CloseEntry();

                    ZipEntry ZE = new ZipEntry($"wg.rv.{portInt}.conf");
                    ZE.AESKeySize = 256;
                    ZE.Comment = "Wireguard configuration";
                    //ZE.IsCrypted = true;
                    ZOT.PutNextEntry(ZE);
                    ZOT.Write(Encoding.UTF8.GetBytes(confoutTheirs.Text));
                    ZOT.CloseEntry();
                    ZipEntry ZE_SVC = new ZipEntry("rvtunsvc.xml");
                    ZE_SVC.AESKeySize = 256;
                    ZE_SVC.Comment = "Service coniguration";
                    ZOT.PutNextEntry(ZE_SVC);

                    /* Create the SM file */
                    var XS = new XmlWriterSettings() {
                        Indent = true,
                        NewLineChars = "\r\n"
                    };
                    var XW = XmlWriter.Create(ZOT);
                    XW.WriteStartElement("service");
                    XW.WriteStartElement("id");
                    XW.WriteString($"RV-TunnelService-{portInt.ToString()}");
                    XW.WriteEndElement();
                    XW.WriteStartElement("name");
                    XW.WriteString($"RV-TunnelService-{portInt.ToString()}");
                    XW.WriteEndElement();
                    XW.WriteStartElement("executable");
                    XW.WriteString($"powershell");
                    XW.WriteEndElement();
                    XW.WriteStartElement("arguments");
                    XW.WriteString($"-ExecutionPolicy Bypass .\\{portnumber.Text}.service.ps1");
                    XW.WriteEndElement();
                    //XW.WriteStartElement("workingdirectory");
                    //XW.WriteString(Path.Combine("", "tunnels", portInt.ToString()));
                    //XW.WriteEndElement();
                    XW.WriteStartElement("description");
                    XW.WriteString($"Secure WebRTC based end-to-end tunnel port: {portInt}.");
                    XW.WriteEndElement();

                    XW.WriteStartElement("log");
                    XW.WriteStartAttribute("mode");
                    XW.WriteString("roll");
                    XW.WriteEndAttribute();
                    XW.WriteEndElement();
                    XW.WriteEndElement();
                    XW.Flush();

                    ZipEntry ZE_PS = new ZipEntry($"{portnumber.Text}.service.ps1");
                    ZE_PS.AESKeySize = 256;
                    ZE_PS.Comment = "Service Powershell Script (Win32/Win64)";
                    ZOT.CloseEntry();
                    ZipEntry ZE_WG_T = new ZipEntry($"wg.rv.{portInt}.conf");
                    ZE_WG_T.AESKeySize = 256;
                    ZOT.PutNextEntry(ZE_WG_T);
                    
                    ZOT.Write(Encoding.UTF8.GetBytes(confoutTheirs.Text));
                    ZOT.CloseEntry();
                    ZOT.PutNextEntry(ZE_PS);
                    string runCommandTheirs = role.SelectedItem == 0 ? "..\\..\\o-l.exe" : "..\\..\\a-c.exe";
                    runCommandTheirs += " tunnel.toml";
                    string powershellScriptTheirs = "do {\r\n" +
                    $"{runCommandTheirs}\r\n" +
                    $"Start-Sleep -Seconds 2\r\n" +
                    "}\r\n" +
                    "until ($false)";
                    ZOT.Write(Encoding.UTF8.GetBytes(powershellScriptTheirs));

                    /* Finish ZIP here */
                    ZOT.CloseEntry();

                    


                    ZipEntry config = new ZipEntry("config.toml");
                    config.AESKeySize = 256;
                    
                    ZOT.PutNextEntry(config);
                    Utils.ConfigOut co = new Utils.ConfigOut() {
                        ServiceConfigXmlFileName = "rvtunsvc.xml",
                        ServicePowershellScript = $"{portnumber.Text}.service.ps1",
                        WebRtcForwarderConfigurationFileName = "tunnel.conf",
                        WireguardConfigName = $"wg.rv.{portInt}.conf",
                        PortNumber = portInt,
                    };
                    string config_toml = Toml.FromModel(co.ToTomlTable());
                    MessageBox.Query("TOML", config_toml, "OK");
                    ZOT.Write(Encoding.UTF8.GetBytes(config_toml));
                    ZOT.Flush();
                    ZOT.CloseEntry();
                    if(role.SelectedItem == 1)
                    {
                        AddAddressFilteredPortForwarderConfiguration(ZOT, [AddressS, AddressS6], [AddressSSubnet, AddressS6Subnet], portInt);
                    }
                    ZOT.Close();

                    var ZOO = new ZipOutputStream(File.Create($".\\{portnumber.Text}.tun.ourside.zip.rvtunnelconfiguration"));
                    ZOO.Password = random128bitsHumanFriendly;
                    
                    ZipEntry ZE_FW_O = new ZipEntry("tunnel.toml");
                    ZE_FW_O.AESKeySize = 256;

                    ZOO.PutNextEntry(ZE_FW_O);
                    
                    ZOO.Write(Encoding.UTF8.GetBytes(ourForwarderToml));
                    ZOO.CloseEntry();
                    
                    ZipEntry ZE_PS_O = new ZipEntry($"{portnumber.Text}.service.ps1");
                    ZE_PS_O.AESKeySize = 256;
                    ZOO.PutNextEntry(ZE_PS_O);

                    string runCommandOurs = role.SelectedItem == 1 ? "..\\..\\o-l.exe" : "..\\..\\a-c.exe";
                    runCommandOurs += " tunnel.toml";
                    string powershellScriptOurs = "do {\r\n" +
                    $"{runCommandOurs}\r\n" +
                    $"Start-Sleep -Seconds 2\r\n" +
                    "}\r\n" +
                    "until ($false)";
                    ZOO.Write(Encoding.UTF8.GetBytes(powershellScriptOurs));
                    ZOO.CloseEntry();

                    

                    ZipEntry ZE_XWO = new ZipEntry("rvtunsvc.xml");
                    ZE_XWO.AESKeySize = 256;
                    ZOO.PutNextEntry(ZE_XWO);

                    var XWO = XmlWriter.Create(ZOO);
                    XWO.WriteStartElement("service");
                    XWO.WriteStartElement("id");
                    XWO.WriteString($"RV-TunnelService-{portInt}");
                    XWO.WriteEndElement();
                    XWO.WriteStartElement("name");
                    XWO.WriteString($"RV-TunnelService-{portInt}");
                    XWO.WriteEndElement();
                    XWO.WriteStartElement("executable");
                    XWO.WriteString($"powershell");
                    XWO.WriteEndElement();
                    XWO.WriteStartElement("arguments");
                    XWO.WriteString($"-ExecutionPolicy Bypass .\\{portnumber.Text}.service.ps1");
                    XWO.WriteEndElement();
                    //XWO.WriteStartElement("workingdirectory");
                    //XWO.WriteString(Path.Combine("", "tunnels", portInt.ToString()));
                    //XWO.WriteEndElement();
                    XWO.WriteStartElement("description");
                    XWO.WriteString($"Secure WebRTC based end-to-end tunnel port: {portInt}.");
                    XWO.WriteEndElement();

                    XWO.WriteStartElement("log");
                    XWO.WriteStartAttribute("mode");
                    XWO.WriteString("roll");
                    XWO.WriteEndAttribute();
                    XWO.WriteEndElement();
                    XWO.WriteEndElement();
                    XWO.Flush();
                    XWO.Close();
                    ZOO.CloseEntry();

                    ZipEntry ZE_WG_O = new ZipEntry($"wg.rv.{portInt}.conf");
                    ZE_WG_O.AESKeySize = 256;
                    ZOO.PutNextEntry(ZE_WG_O);
                    
                    ZOO.Write(Encoding.UTF8.GetBytes(confout.Text));
                    ZOO.CloseEntry();

                    

                    ZipEntry ZE_config_O = new ZipEntry("config.toml");
                    ZE_config_O.AESKeySize = 256;
                    ZOO.PutNextEntry(ZE_config_O);

                    Utils.ConfigOut coo = new Utils.ConfigOut() {
                        ServiceConfigXmlFileName = "rvtunsvc.xml",
                        ServicePowershellScript = $"{portnumber.Text}.service.ps1",
                        WebRtcForwarderConfigurationFileName = "tunnel.toml",
                        WireguardConfigName = $"wg.rv.{portInt}.conf",
                        PortNumber = portInt
                    };
                    string configout = (Toml.FromModel(coo.ToTomlTable()));
                    ZOO.Write(Encoding.UTF8.GetBytes(configout));
                    ZOO.Flush();
                    
                    ZOO.CloseEntry();
                    if (role.SelectedItem == 0)
                    {
                        AddAddressFilteredPortForwarderConfiguration(ZOO, [AddressS, AddressS6], [AddressSSubnet, AddressS6Subnet], portInt);
                    }
                   

                    ZOO.Close();

                    MessageBox.Query("Generated conf:", Toml.FromModel((new Utils.ForwarderConfigOut()
                    {
                        Address = "127.0.0.1",
                        PublishAuthUser = randomUsername,
                        PublishAuthPass = randomPassword,
                        PeerPSK = randomPeerPSK,
                        PublishEndpoint = $"wss://vz.al/anonwsmul/{randomSessionName}/wsa",
                        Port = portnumber.Text,
                        PublishAuthType = "Basic",
                        Type = "UDP",
                        WebRTCMode = "Offer",

                    }).ToTomlTable()), "Ok");



                }
                catch (System.Exception E)
                {
                    MessageBox.Query(75, 24, "Exception", $"{E.ToString()}, {E.StackTrace}");
                }
            };
        }
    }
}
