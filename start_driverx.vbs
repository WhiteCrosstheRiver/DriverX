Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
folder = fso.GetParentFolderName(WScript.ScriptFullName)
exe = fso.BuildPath(folder, "dist\DriverX\DriverX.exe")
If fso.FileExists(exe) Then
  shell.CurrentDirectory = fso.GetParentFolderName(exe)
  shell.Run Chr(34) & exe & Chr(34), 1, False
Else
  MsgBox "DriverX build not found. Run build_native.ps1 first.", 48, "DriverX"
End If
