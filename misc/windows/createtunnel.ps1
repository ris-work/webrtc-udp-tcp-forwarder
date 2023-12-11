$tet = Read-Host "Tunnel Endpoint Type [o-l|a-c]"

if ($tet -eq "a-c" -or $tet -eq "o-l"){
    $ten = Read-Host "Tunnel Endpoint Name"
    $teip = Read-Host "IP Addresses [WG] [CIDR]"
    $teport = Read-Host "Tunnel port"
    $peerpsk = Read-Host "Peer PSK"
    $PrivKey = $(wg genkey)
    $PubKey = $PrivKey | wg genkey
    Echo "Great! Our public key is: $PubKey"
    Echo "Please email/IM it to whoever requested you to run this script."
    $conf = "[Interface]"
    $conf += "`nPrivateKey = $key"
    if ($tet -eq "a-c"){
        $conf += "`nListenPort = $teport"
    }
    $conf += "`n`n[Peer]"
    $conf += "`nAllowedIPs = $teip"
    $PeerPubKey = Read-Host "Peer Public Key"
    $conf += "`nPublicKey = $PeerPublicKey"
    $conf += "`nPersistentKeepalive = 2"
    echo $conf
}
else {
    Echo "Restart the program and mention either 'o-l' or 'a-c'"
}