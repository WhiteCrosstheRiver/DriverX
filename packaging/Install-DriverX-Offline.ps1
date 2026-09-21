# DriverX offline installer. Keep DriverX.exe and winfsp-*.msi beside this script.
$ErrorActionPreference = 'Stop'
$source = Split-Path -Parent $MyInvocation.MyCommand.Path
$target = Join-Path ${env:ProgramFiles} 'DriverX'
$msi = Get-ChildItem $source -Filter 'winfsp-*.msi' | Select-Object -First 1
if(-not $msi) { throw '离线包缺少 WinFsp MSI。请使用 build_release.ps1 生成完整离线包。' }
$winfsp = (Test-Path 'C:\Program Files (x86)\WinFsp\bin\winfsp-x64.dll') -or (Test-Path 'C:\Program Files\WinFsp\bin\winfsp-x64.dll')
Write-Progress -Activity '安装 DriverX' -Status '检查 WinFsp…' -PercentComplete 15
if(-not $winfsp) { $p=Start-Process msiexec.exe -ArgumentList @('/i',$msi.FullName,'/passive','/norestart') -Wait -PassThru; if($p.ExitCode -notin @(0,3010)){throw "WinFsp 安装失败，错误码 $($p.ExitCode)"} }
Write-Progress -Activity '安装 DriverX' -Status '复制程序…' -PercentComplete 65
New-Item -ItemType Directory -Path $target -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $source 'DriverX.exe') -Destination (Join-Path $target 'DriverX.exe') -Force
$shell=New-Object -ComObject WScript.Shell
$shortcut=$shell.CreateShortcut((Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\DriverX.lnk'))
$shortcut.TargetPath=Join-Path $target 'DriverX.exe';$shortcut.WorkingDirectory=$target;$shortcut.Save()
Write-Progress -Activity '安装 DriverX' -Status '完成' -PercentComplete 100
Start-Process (Join-Path $target 'DriverX.exe')
