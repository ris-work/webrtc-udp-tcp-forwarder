#Privileged run
$exename = ""
$extension = ""
$iconpath = ""
$formatdesc = ""
$appname = ""


New-PSDrive -PSProvider registry -Root HKEY_CLASSES_ROOT -Name HKCR
mkdir "HKCU:\software\microsoft\windows\CurrentVersion\App Paths\$exename"
$a = @{
    Path = "HKCU:\software\microsoft\windows\CurrentVersion\App Paths\$exename"
    Name = "(default)"
    PropertyType = "String"
    Value = ""
}
New-ItemProperty @a

mkdir "HKCR:\Applications\$exename"
$a = @{
    Path = "HKCR:\Applications\$exename\$exename"
    Name = "(default)"
    PropertyType = "String"
    Value = ""
}
New-ItemProperty -Force @a

mkdir "HKCR:\Applications\$exename\DefaultIcon"
$a = @{
    Path = "HKCR:\Applications\$exename\DefaultIcon\(default)"
    Name = "(default)"
    PropertyType = "String"
    Value = "$pwd\$iconpath"
}
New-ItemProperty -Force @a

mkdir "HKCR:\Applications\$exename\SupportedTypes"
$a = @{
    Path = "HKCR:\Applications\$exename\SupportedTypes\"
    Name = "$extension"
    PropertyType = "String"
    Value = ""
}
New-ItemProperty -Force @a
mkdir "HKCR:\Applications\$exename\shell"
mkdir "HKCR:\Applications\$exename\shell\open"
mkdir "HKCR:\Applications\$exename\shell\open\command"

$a = @{
    Path = "HKCR:\Applications\$exename\shell\open\command"
    Name = "(default)"
    PropertyType = "String"
    Value = "`"$pwd\$exename`" `"%1`""
}
New-ItemProperty -Force @a


mkdir "HKCR:\$extension"
mkdir "HKCR:\$extension\shell"
mkdir "HKCR:\$extension\shell\open"
mkdir "HKCR:\$extension\shell\open\command"

$a = @{
    Path = "HKCR:\$extension\shell\open\command"
    Name = "(default)"
    PropertyType = "String"
    Value = "`"$pwd\$exename`" `"%1`""
}
New-ItemProperty -Force @a

$a = @{
    Path = "HKCR:\$extension\"
    Name = "(default)"
    PropertyType = "String"
    Value = "$appname"
}
New-ItemProperty -Force @a


mkdir "HKCR:\$appname"
mkdir "HKCR:\$appname\DefaultIcon"
mkdir "HKCR:\$appname\shell"
mkdir "HKCR:\$appname\shell\open"
mkdir "HKCR:\$appname\shell\open\command"


$a = @{
    Path = "HKCR:\$appname\"
    Name = "(default)"
    PropertyType = "String"
    Value = "$formatdesc"
    #Value = "`"$pwd\$exename`" `"%1`""
}
New-ItemProperty -Force @a

$a = @{
    Path = "HKCR:\$appname\shell\open\command"
    Name = "(default)"
    PropertyType = "String"
    Value = "`"$pwd\$exename`" `"%1`""
}
New-ItemProperty -Force @a

$a = @{
    Path = "HKCR:\$appname\DefaultIcon"
    Name = "(default)"
    PropertyType = "String"
    Value = "$pwd\$iconpath"
}
New-ItemProperty -Force @a

mkdir "HKCR:\$extension\DefaultIcon"
$a = @{
    Path = "HKCR:\$extension\DefaultIcon\"
    Name = "(default)"
    PropertyType = "String"
    Value = "$pwd\$iconpath"
}
New-ItemProperty -Force @a


mkdir "HKCR:\$appname\DefaultIcon"
$a = @{
    Path = "HKCR:\$appname\DefaultIcon\"
    Name = "(default)"
    PropertyType = "String"
    Value = "$pwd\$iconpath"
}
New-ItemProperty -Force @a

mkdir "HKCR:\$appname\shell"
mkdir "HKCR:\$appname\shell\open"
mkdir "HKCR:\$appname\shell\open\command"

$a = @{
    Path = "HKCR:\$appname\"
    Name = "(default)"
    PropertyType = "String"
    Value = "$formatdesc"
}
New-ItemProperty -Force @a

$a = @{
    Path = "HKCU:\Software\Classes\$appname\shell\open\command"
    Name = "(default)"
    PropertyType = "String"
    Value = "`"$pwd\$exename`" `"%1`""
}
New-ItemProperty -Force @a

#HKCU things
mkdir "HKCU:\Software\Classes\Applications\$exename"
$a = @{
    Path = "HKCU:\Software\Classes\Applications\$exename\$exename"
    Name = "(default)"
    PropertyType = "String"
    Value = ""
    #Value = "$pwd\$exename"
}
New-ItemProperty -Force @a

mkdir "HKCU:\Software\Classes\Applications\$exename\DefaultIcon"
$a = @{
    Path = "HKCU:\Software\Classes\Applications\$exename\DefaultIcon\"
    Name = "(default)"
    PropertyType = "String"
    Value = "$pwd\$iconpath"
}
New-ItemProperty -Force @a

mkdir "HKCU:\Software\Classes\Applications\$exename\SupportedTypes"
$a = @{
    Path = "HKCU:\Software\Classes\Applications\$exename\SupportedTypes\"
    Name = "$extension"
    PropertyType = "String"
    Value = ""
}
New-ItemProperty -Force @a
mkdir "HKCU:\Software\Classes\Applications\$exename\shell"
mkdir "HKCU:\Software\Classes\Applications\$exename\shell\open"
mkdir "HKCU:\Software\Classes\Applications\$exename\shell\open\command"

$a = @{
    Path = "HKCU:\Software\Classes\Applications\$exename\shell\open\command"
    Name = "(default)"
    PropertyType = "String"
    Value = "`"$pwd\$exename`" `"%1`""
}
New-ItemProperty -Force @a


mkdir "HKCU:\Software\Classes\$extension"
mkdir "HKCU:\Software\Classes\$extension\shell"
mkdir "HKCU:\Software\Classes\$extension\shell\open"
mkdir "HKCU:\Software\Classes\$extension\shell\open\command"

$a = @{
    Path = "HKCU:\Software\Classes\$extension\shell\open\command"
    Name = "(default)"
    PropertyType = "String"
    Value = "`"$pwd\$exename`" `"%1`""
}
New-ItemProperty -Force @a

$a = @{
    Path = "HKCU:\Software\Classes\$extension\"
    Name = "(default)"
    PropertyType = "String"
    Value = "$appname"
}
New-ItemProperty -Force @a


mkdir "HKCU:\Software\Classes\$appname"
$a = @{
    Path = "HKCU:\Software\Classes\$appname\"
    Name = "(default)"
    PropertyType = "String"
    Value = "$formatdesc"
    #Value = "`"$pwd\$exename`" `"%1`""
}
New-ItemProperty -Force @a

mkdir "HKCU:\Software\Classes\$extension\DefaultIcon"
$a = @{
    Path = "HKCU:\Software\Classes\$extension\DefaultIcon\"
    Name = "(default)"
    PropertyType = "String"
    Value = "$pwd\$iconpath"
}
New-ItemProperty -Force @a


mkdir "HKCU:\Software\Classes\$appname\DefaultIcon"
$a = @{
    Path = "HKCU:\Software\Classes\$appname\DefaultIcon\"
    Name = "(default)"
    PropertyType = "String"
    Value = "$pwd\$iconpath"
}
New-ItemProperty -Force @a

mkdir "HKCU:\Software\Classes\$appname\shell"
mkdir "HKCU:\Software\Classes\$appname\shell\open"
mkdir "HKCU:\Software\Classes\$appname\shell\open\command"

$a = @{
    Path = "HKCU:\Software\Classes\$appname\shell\open\command"
    Name = "(default)"
    PropertyType = "String"
    Value = "`"$pwd\$exename`" `"%1`""
}
New-ItemProperty -Force @a
$a = @{
    Path = "HKCU:\Software\Classes\$appname\"
    Name = "(default)"
    PropertyType = "String"
    Value = "$formatdesc"
}
New-ItemProperty -Force @a

#FILE ASSOCIATION
#LOCAL USER

mkdir "HKCU:\Software\Classes\$extension\$appname"
mkdir "HKCU:\Software\Classes\$extension\$appname\ShellNew"
$a = @{
    Path = "HKCU:\Software\Classes\$extension\$appname\ShellNew"
    Name = "FileName"
    PropertyType = "String"
    Value = "$pwd\new.logs.sqlite3$extension"
}
New-ItemProperty -Force @a

#SYSTEM

mkdir "HKCR:\$extension\$appname"
mkdir "HKCR:\$extension\$appname\ShellNew"
$a = @{
    Path = "HKCR:\Software\Classes\$extension\$appname\ShellNew"
    Name = "FileName"
    PropertyType = "String"
    Value = "$pwd\new.logs.sqlite3$extension"
}
New-ItemProperty -Force @a

#Control panel things
$guid = Get-Content "guid.guid"
mkdir "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\ControlPanel\NameSpace\$guid"
mkdir "HKCR:\CLSID\$guid"

$a = @{
    Path = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\ControlPanel\NameSpace\$guid"
    Name = "(default)"
    PropertyType = "String"
    Value = "Health Monitor Log Viewer"
}
New-ItemProperty -Force @a

$a = @{
    Path = "HKCR:\CLSID\$guid"
    Name = "LocalizedString"
    PropertyType = "String"
    Value = "Health Monitor Log Viewer"
}
New-ItemProperty -Force @a

$a = @{
    Path = "HKCR:\CLSID\$guid"
    Name = "InfoTip"
    PropertyType = "String"
    Value = "Health Monitor Log Viewer"
}
New-ItemProperty -Force @a

$a = @{
    Path = "HKCR:\CLSID\$guid"
    Name = "System.ApplicationName"
    PropertyType = "String"
    Value = "$appname"
}
New-ItemProperty -Force @a

$a = @{
    Path = "HKCR:\CLSID\$guid"
    Name = "System.ControlPanel.Category"
    PropertyType = "String"
    Value = "0,2,3,8"
}
New-ItemProperty -Force @a

mkdir "HKCR:\CLSID\$guid\DefaultIcon\"
$a = @{
    Path = "HKCR:\CLSID\$guid\DefaultIcon\"
    Name = "(default)"
    PropertyType = "String"
    Value = "$pwd\time-view.ico"
}
New-ItemProperty -Force @a


mkdir "HKCR:\CLSID\$guid\shell"
mkdir "HKCR:\CLSID\$guid\shell\open"
mkdir "HKCR:\CLSID\$guid\shell\open\command"
$a = @{
    Path = "HKCR:\CLSID\$guid\DefaultIcon\"
    Name = "(default)"
    PropertyType = "String"
    Value = "$pwd\$exename"
}
New-ItemProperty -Force @a

winget install SQLite.SQLite --scope Machine
$SQLite3ExePathMachine = (Get-Command "sqlite3.exe" | Select -Last 1).Path

$EWS = "Edit with SQLite3"
mkdir "HKCU:\Software\Classes\$extension"
mkdir "HKCU:\Software\Classes\$extension\shell"
mkdir "HKCU:\Software\Classes\$extension\shell\$EWS"
mkdir "HKCU:\Software\Classes\$extension\shell\$EWS\command"
mkdir "HKCU:\Software\Classes\$appname"
mkdir "HKCU:\Software\Classes\$appname\shell"
mkdir "HKCU:\Software\Classes\$appname\shell\$EWS"
mkdir "HKCU:\Software\Classes\$appname\shell\$EWS\command"

mkdir "HKCR:\$extension"
mkdir "HKCR:\$extension\shell"
mkdir "HKCR:\$extension\shell\$EWS"
mkdir "HKCR:\$extension\shell\$EWS\command"
mkdir "HKCR:\$appname"
mkdir "HKCR:\$appname\shell"
mkdir "HKCR:\$appname\shell\$EWS"
mkdir "HKCR:\$appname\shell\$EWS\command"

$a = @{
    Path = "HKCU:\Software\Classes\$extension\shell\$EWS\command"
    Name = "(default)"
    PropertyType = "String"
    Value = "`"$SQLite3ExePathMachine`" `"%1`""
}
New-ItemProperty -Force @a

$a = @{
    Path = "HKCU:\Software\Classes\$appname\shell\$EWS\command"
    Name = "(default)"
    PropertyType = "String"
    Value = "`"$SQLite3ExePathMachine`" `"%1`""
}
New-ItemProperty -Force @a

winget install SQLite.SQLite --scope User
$SQLite3ExePathLocal = ((Get-Command "sqlite3.exe") | Select -First 1).Path
$a = @{
    Path = "HKCR:\$extension\shell\$EWS\command"
    Name = "(default)"
    PropertyType = "String"
    Value = "`"$SQLite3ExePathLocal`" `"%1`""
}
New-ItemProperty -Force @a


$a = @{
    Path = "HKCR:\$appname\shell\$EWS\command"
    Name = "(default)"
    PropertyType = "String"
    Value = "`"$SQLite3ExePathLocal`" `"%1`""
}
New-ItemProperty -Force @a
