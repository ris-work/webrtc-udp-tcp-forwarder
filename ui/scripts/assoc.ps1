#Privileged run
#$exename = ""
#$extension = ""
#$iconpath = ""
#$formatdesc = ""
#$appname = ""


New-PSDrive -PSProvider registry -Root HKEY_CLASSES_ROOT -Name HKCR
mkdir "HKCU:\software\microsoft\windows\CurrentVersion\App Paths\$exename"
$a = @{
    Path = "HKCU:\software\microsoft\windows\CurrentVersion\App Paths\$exename"
    Name = "(default)"
    PropertyType = "String"
    Value = ""
}
New-ItemProperty @a -Force

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
    Path = "HKCR:\Applications\$exename\DefaultIcon\"
    Name = "(default)"
    PropertyType = "String"
    Value = "$working_directory\$iconpath"
}
New-ItemProperty -Force @a

mkdir "HKCR:\Applications\$exename\SupportedTypes"
$a = @{
    Path = "HKCR:\Applications\$exename\SupportedTypes\"
    Name = "$extension"
    PropertyType = "String"
    #Value = ""
}
New-ItemProperty -Force @a
mkdir "HKCR:\Applications\$exename\shell"
mkdir "HKCR:\Applications\$exename\shell\open"
mkdir "HKCR:\Applications\$exename\shell\open\command"

$a = @{
    Path = "HKCR:\Applications\$exename\shell\open\command"
    Name = "(default)"
    PropertyType = "String"
    Value = "`"$working_directory\$exename`" `"%1`""
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
    Value = "`"$working_directory\$exename`" `"%1`""
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
    #Value = "`"$working_directory\$exename`" `"%1`""
}
New-ItemProperty -Force @a

$a = @{
    Path = "HKCR:\$appname\shell\open\command"
    Name = "(default)"
    PropertyType = "String"
    Value = "`"$working_directory\$exename`" `"%1`""
}
New-ItemProperty -Force @a

$a = @{
    Path = "HKCR:\$appname\DefaultIcon"
    Name = "(default)"
    PropertyType = "String"
    Value = "$working_directory\$iconpath"
}
New-ItemProperty -Force @a

mkdir "HKCR:\$extension\DefaultIcon"
$a = @{
    Path = "HKCR:\$extension\DefaultIcon\"
    Name = "(default)"
    PropertyType = "String"
    Value = "$working_directory\$iconpath"
}
New-ItemProperty -Force @a


mkdir "HKCR:\$appname\DefaultIcon"
$a = @{
    Path = "HKCR:\$appname\DefaultIcon\"
    Name = "(default)"
    PropertyType = "String"
    Value = "$working_directory\$iconpath"
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
    Value = "`"$working_directory\$exename`" `"%1`""
}
New-ItemProperty -Force @a

#HKCU things
mkdir "HKCU:\Software\Classes\Applications\$exename"
$a = @{
    Path = "HKCU:\Software\Classes\Applications\$exename\$exename"
    Name = "(default)"
    PropertyType = "String"
    #Value = ""
    #Value = "$working_directory\$exename"
}
New-ItemProperty -Force @a

mkdir "HKCU:\Software\Classes\Applications\$exename\DefaultIcon"
$a = @{
    Path = "HKCU:\Software\Classes\Applications\$exename\DefaultIcon\"
    Name = "(default)"
    PropertyType = "String"
    Value = "$working_directory\$iconpath"
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
    Value = "`"$working_directory\$exename`" `"%1`""
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
    Value = "`"$working_directory\$exename`" `"%1`""
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
    #Value = "`"$working_directory\$exename`" `"%1`""
}
New-ItemProperty -Force @a

mkdir "HKCU:\Software\Classes\$extension\DefaultIcon"
$a = @{
    Path = "HKCU:\Software\Classes\$extension\DefaultIcon\"
    Name = "(default)"
    PropertyType = "String"
    Value = "$working_directory\$iconpath"
}
New-ItemProperty -Force @a


mkdir "HKCU:\Software\Classes\$appname\DefaultIcon"
$a = @{
    Path = "HKCU:\Software\Classes\$appname\DefaultIcon\"
    Name = "(default)"
    PropertyType = "String"
    Value = "$working_directory\$iconpath"
}
New-ItemProperty -Force @a

mkdir "HKCU:\Software\Classes\$appname\shell"
mkdir "HKCU:\Software\Classes\$appname\shell\open"
mkdir "HKCU:\Software\Classes\$appname\shell\open\command"

$a = @{
    Path = "HKCU:\Software\Classes\$appname\shell\open\command"
    Name = "(default)"
    PropertyType = "String"
    Value = "`"$working_directory\$exename`" `"%1`""
}
New-ItemProperty -Force @a
$a = @{
    Path = "HKCU:\Software\Classes\$appname\"
    Name = "(default)"
    PropertyType = "String"
    Value = "$formatdesc"
}
New-ItemProperty -Force @a



mkdir "HKLM:\Software\Classes\$extension"
mkdir "HKLM:\Software\Classes\$extension\DefaultIcon"
$a = @{
    Path = "HKLM:\Software\Classes\$extension\DefaultIcon\"
    Name = "(default)"
    PropertyType = "String"
    Value = "$working_directory\$iconpath"
}
New-ItemProperty -Force @a

mkdir "HKCR:\$extension"
mkdir "HKCR:\$extension\DefaultIcon"
$a = @{
    Path = "HKCR:\$extension\DefaultIcon\"
    Name = "(default)"
    PropertyType = "String"
    Value = "$working_directory\$iconpath"
}
New-ItemProperty -Force @a

mkdir "HKLM:\Software\Classes\$extension\"
mkdir "HKLM:\Software\Classes\$extension\DefaultIcon"
$a = @{
    Path = "HKLM:\Software\Classes\$extension\DefaultIcon\"
    Name = "(default)"
    PropertyType = "String"
    Value = "$working_directory\$iconpath"
}
New-ItemProperty -Force @a

mkdir "HKLM:\Software\Classes\$extension\shell"
mkdir "HKLM:\Software\Classes\$extension\shell\open"
mkdir "HKLM:\Software\Classes\$extension\shell\open\command"

$a = @{
    Path = "HKLM:\Software\Classes\$extension\shell\open\command"
    Name = "(default)"
    PropertyType = "String"
    Value = "`"$working_directory\$exename`" `"%1`""
}
New-ItemProperty -Force @a
$a = @{
    Path = "HKLM:\Software\Classes\$extension\"
    Name = "(default)"
    PropertyType = "String"
    Value = "$formatdesc"
}
New-ItemProperty -Force @a


mkdir "HKCR:\$extension\"
mkdir "HKCR:\$extension\DefaultIcon"
$a = @{
    Path = "HKCR:\$extension\DefaultIcon\"
    Name = "(default)"
    PropertyType = "String"
    Value = "$working_directory\$iconpath"
}
New-ItemProperty -Force @a

mkdir "HKCR:\$extension\shell"
mkdir "HKCR:\$extension\shell\open"
mkdir "HKCR:\$extension\shell\open\command"

$a = @{
    Path = "HKCR:\$extension\shell\open\command"
    Name = "(default)"
    PropertyType = "String"
    Value = "`"$working_directory\$exename`" `"%1`""
}
New-ItemProperty -Force @a
$a = @{
    Path = "HKCR:\$extension\"
    Name = "(default)"
    PropertyType = "String"
    Value = "$formatdesc"
}
New-ItemProperty -Force @a


mkdir "HKLM:\Software\Classes\Applications\$exename"
$a = @{
    Path = "HKLM:\Software\Classes\Applications\$exename\"
    Name = "(default)"
    PropertyType = "String"
    #Value = ""
    #Value = "$working_directory\$exename"
}
New-ItemProperty -Force @a

mkdir "HKLM:\Software\Classes\Applications\$exename\DefaultIcon"
$a = @{
    Path = "HKLM:\Software\Classes\Applications\$exename\DefaultIcon\"
    Name = "(default)"
    PropertyType = "String"
    Value = "$working_directory\$iconpath"
}
New-ItemProperty -Force @a

mkdir "HKLM:\Software\Classes\Applications\$exename\SupportedTypes"
$a = @{
    Path = "HKLM:\Software\Classes\Applications\$exename\SupportedTypes\"
    Name = "$extension"
    PropertyType = "String"
    Value = ""
}
New-ItemProperty -Force @a
mkdir "HKLM:\Software\Classes\Applications\$exename\shell"
mkdir "HKLM:\Software\Classes\Applications\$exename\shell\open"
mkdir "HKLM:\Software\Classes\Applications\$exename\shell\open\command"

$a = @{
    Path = "HKLM:\Software\Classes\Applications\$exename\shell\open\command"
    Name = "(default)"
    PropertyType = "String"
    Value = "`"$working_directory\$exename`" `"%1`""
}
New-ItemProperty -Force @a

mkdir "HKLM:\software\microsoft\windows\CurrentVersion\App Paths\$exename"
$a = @{
    Path = "HKCU:\software\microsoft\windows\CurrentVersion\App Paths\$exename"
    Name = "(default)"
    PropertyType = "String"
    #Value = ""
}
New-ItemProperty @a -Force
