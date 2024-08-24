function AdminsOnlyWithOthersRX($filename){
$acl = new-object System.Security.AccessControl.DirectorySecurity
$IR= New-Object System.Security.Principal.NTAccount("Administrator")
$RuleSystemFullControl = New-Object System.Security.AccessControl.FileSystemAccessRule("SYSTEM", "FullControl", "Allow");
$RuleAdministratorsFullControl = New-Object System.Security.AccessControl.FileSystemAccessRule("Administrators", "FullControl", "Allow");
$RuleUsersRX = New-Object System.Security.AccessControl.FileSystemAccessRule("Users", "ReadAndExecute", "Allow");
$acl.SetOwner($IR);
$acl.AddAccessRule($RuleSystemFullControl);
$acl.AddAccessRule($RuleAdministratorsFullControl);
$acl.AddAccessRule($RuleUsersRX);
$acl.SetAccessRuleProtection($true, $false);
$acl | Set-Acl $filename
}
function AdminsOnly($filename){
$acl = new-object System.Security.AccessControl.DirectorySecurity
$IR= New-Object System.Security.Principal.NTAccount("Administrator")
$RuleSystemFullControl = New-Object System.Security.AccessControl.FileSystemAccessRule("SYSTEM", "FullControl", "Allow");
$RuleAdministratorsFullControl = New-Object System.Security.AccessControl.FileSystemAccessRule("Administrators", "FullControl", "Allow");
$acl.SetOwner($IR);
$acl.AddAccessRule($RuleSystemFullControl);
$acl.AddAccessRule($RuleAdministratorsFullControl);
$acl.SetAccessRuleProtection($true, $false);
$acl | Set-Acl $filename
}