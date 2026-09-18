# DriverX online installer. Run from the folder containing DriverX.exe.
$ErrorActionPreference = 'Stop'
$source = Split-Path -Parent $MyInvocation.MyCommand.Path
$target = Join-Path ${env:ProgramFiles} 'DriverX'
$winfspUrl = 'https://github.com/winfsp/winfsp/releases/download/v2.1/winfsp-2.1.25156.msi'

function Step([string]$text,[int]$percent) { Write-Progress -Activity '安装 DriverX' -Status $text -PercentComplete $percent }
function Test-WinFsp { Test-Path 'C:\Program Files (x86)\WinFsp\bin\winfsp-x64.dll' -or Test-Path 'C:\Program Files\WinFsp\bin\winfsp-x64.dll' }

if(-not (Test-WinFsp)) {
  Step '下载 WinFsp 文件系统驱动…' 15
  $msi = Join-Path $env:TEMP 'DriverX-WinFsp.msi'
  Invoke-WebRequest -Uri $winfspUrl -OutFile $msi
  Step '安装 WinFsp…' 35
  $p = Start-Process msiexec.exe -ArgumentList @('/i',$msi,'/passive','/norestart') -Wait -PassThru
  Remove-Item -LiteralPath $msi -Force -ErrorAction SilentlyContinue
  if($p.ExitCode -notin @(0,3010)) { throw "WinFsp 安装失败，错误码 $($p.ExitCode)" }
}

Step '复制 DriverX 和内置 rclone…' 65
New-Item -ItemType Directory -Path $target -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $source 'DriverX.exe') -Destination (Join-Path $target 'DriverX.exe') -Force
Step '创建开始菜单快捷方式…' 85
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\DriverX.lnk'))
$shortcut.TargetPath = Join-Path $target 'DriverX.exe'; $shortcut.WorkingDirectory = $target; $shortcut.Save()
Step '完成' 100
Start-Process (Join-Path $target 'DriverX.exe')
