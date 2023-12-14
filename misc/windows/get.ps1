mkdir tunsvc
mkdir tunsvc/dist
cd tunsvc/dist
curl.exe -O https://vz.al/chromebook/webrtc-udp-tcp-forwarder/uv/o-l.exe
curl.exe -O https://vz.al/chromebook/webrtc-udp-tcp-forwarder/uv/a-c.exe

#Third-party
curl.exe https://vz.al/chromebook/webrtc-udp-tcp-forwarder/uv/util-svcbatch.exe -o svcbatch.exe
curl.exe -O https://download.wireguard.com/windows-client/wireguard-installer.exe

#GPL
curl.exe -O https://www.tightvnc.com/download/2.8.81/tightvnc-2.8.81-gpl-setup-64bit.msi

#Scripts
curl.exe -o aclfunctions.ps1 "https://vz.al/chromebook/webrtc-udp-tcp-forwarder/raw?name=misc/windows/aclfunctions.ps1&ci=tip"
curl.exe -o setupfunctions.ps1 "https://vz.al/chromebook/webrtc-udp-tcp-forwarder/raw?name=misc/windows/setupfunctions.ps1&ci=tip"

. ./aclfunctions.ps1
$tunnelroot = "c:\tunsys"
mkdir "$tunnelroot"
AdminsOnlyWithOthersRX("$tunnelroot")
mkdir "$tunnelroot\common"
AdminsOnlyWithOthersRX("$tunnelroot\common")
cp .\o-l.exe "$tunnelroot\common\o-l.exe"
cp .\a-c.exe "$tunnelroot\common\a-c.exe"
cp .\svcbatch.exe "$tunnelroot\common\svcbatch.exe"
AdminsOnlyWithOthersRX("$tunnelroot\common\o-l.exe")
AdminsOnlyWithOthersRX("$tunnelroot\common\a-c.exe")
AdminsOnlyWithOthersRX("$tunnelroot\common\svcbatch.exe")
