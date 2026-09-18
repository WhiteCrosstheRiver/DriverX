Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
base = fso.GetParentFolderName(WScript.ScriptFullName)
exe = fso.BuildPath(base, "dist\DriverX\DriverX.exe")
If fso.FileExists(exe) Then
  shell.Run Chr(34) & exe & Chr(34), 1, False
Else
  MsgBox "DriverX build not found. Run build_native.ps1 first.", 48, "DriverX"
End If
