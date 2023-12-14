. .\aclfunctions.ps1
Function SetupTunnel{
    param($tunnelname, $tunnelroot, $svcbatchdirectory)
    echo "tunnelname: $tunnelname, tunnelroot: $tunnelroot, svcbatch: $svcbatchdirectory"
    cd "$tunnelroot"
    mkdir "$tunnelname"
    AdminsOnlyWithOthersRX($tunnelname)
    cd .\$tunnelname
    mkdir persist
    mkdir tun
    $persistbatchscript = "
    powershell -ExecutionPolicy ByPass $tunnelroot\$tunnelname\persist\run.ps1
    "
    $persistpsscript = "
    do {
    Start-Sleep 10
    wg addconf $tunnelname $tunnelroot\$tunnelname\persist\tunnel.conf
    } until (`$DONE)
    "
    $tunbatchscript = "
    powershell -ExecutionPolicy ByPass $tunnelroot\$tunnelname\tun\run.ps1
    "
    $tunpsscript = "
    do {
    Start-Sleep 2
    ..\..\common\a-c.exe tunnel.toml
    } until (`$DONE)
    "
    AdminsOnlyWithOthersRX("persist")
    cd persist
    mkdir Logs
    AdminsOnlyWithOthersRX("Logs")
    cp $svcbatchdirectory\svcbatch.exe .\
    AdminsOnlyWithOthersRX("SvcBatch.exe")
    echo $persistbatchscript | Out-File -Encoding ASCII run.bat
    echo $persistpsscript | Out-File -Encoding ASCII run.ps1
    AdminsOnlyWithOthersRX("run.bat")
    AdminsOnlyWithOthersRX("run.ps1")
    New-Service -Name "tunsvc: $tunnelname persistence" -BinaryPathName "`"$tunnelroot\$tunnelname\persist\svcbatch.exe`" $tunnelroot\$tunnelname\persist\run.bat"
    cd ..
    mkdir tun
    AdminsOnlyWithOthersRX("tun")
    cd tun
    mkdir Logs
    AdminsOnlyWithOthersRX("Logs")
    cp $svcbatchdirectory\svcbatch.exe .\
    AdminsOnlyWithOthersRX("SvcBatch.exe")
    echo $tunbatchscript | Out-File -Encoding ASCII run.bat
    echo $tunpsscript | Out-File -Encoding ASCII run.ps1
    AdminsOnlyWithOthersRX("run.bat")
    AdminsOnlyWithOthersRX("run.ps1")
    New-Service -Name "tunsvc: $tunnelname" -BinaryPathName "`"$tunnelroot\$tunnelname\tun\svcbatch.exe`" $tunnelroot\$tunnelname\tun\run.bat"
    cd ..
}
