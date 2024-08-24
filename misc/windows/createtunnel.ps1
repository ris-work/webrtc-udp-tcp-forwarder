. .\setupfunctions.ps1
$tet = Read-Host "Tunnel Endpoint Type [o-l|a-c]"
$tunnelroot = "C:\tunsvc"
mkdir $tunnelroot
AdminOnlyButOthersRX $tunnelroot

if ($tet -eq "a-c" -or $tet -eq "o-l"){
    $ten = Read-Host "Tunnel Endpoint Name"
    $teip = Read-Host "IP Addresses [WG] [CIDR]"
    $teport = Read-Host "Tunnel port"
    $peerpsk = Read-Host "Peer PSK"
    #$wgpsk = Read-Host "WG PSK"
    $PrivKey = $(wg genkey)
    $PubKey = $PrivKey | wg genkey
    Echo "Great! Our public key is: $PubKey"
    Echo "Please email/IM it to whoever requested you to run this script."
    SetupTunnel $ten $tunnelroot "$tunnelroot/common/"
    $wg_conf = "[Interface]"
    $wg_conf += "`nPrivateKey = $key"
    if ($tet -eq "a-c"){
        $wg_conf += "`nListenPort = $teport"
    }
    $wg_conf += "`n`n[Peer]"
    $wg_conf += "`nAllowedIPs = $teip"
    $PeerPubKey = Read-Host "Peer Public Key"
    $wg_conf += "`nPublicKey = $PeerPublicKey"
    $wg_conf += "`nPersistentKeepalive = 2"
    #$conf += "`nPersistentKeepalive = 2"
    echo $wg_conf
    Write-Out $wg_conf -File "$tunnelroot/$ten/persist/tunnel.conf"
    AdminOnly "$tunnelroot/$ten/persist/tunnel.conf"
    $t_conf = "Type = `"UDP`""
    if ($tet -eq "a-c"){
        $t_conf = "`nWebRTCMode = `"Accept`""
    }
    else {
        $t_conf = "`nWebRTCMode = `"Offer`""
    }
    $t_conf = "`nAddress = `"127.0.0.1`""
    $t_conf = "`nPort = `"$teport`""
    $t_conf = "`nICEServers = `"['stun:vz.al', 'stun:stun.l.google.com:19302']`""
    Write-Out $t_conf -File "$tunnelroot/$ten/tun/tunnel.toml"
    AdminOnly "$tunnelroot/$ten/tun/tunnel.conf"
}
else {
    Echo "Restart the program and mention either 'o-l' or 'a-c'"
}
