$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'native\DriverX.Desktop\DriverX.Desktop.csproj'
$output = Join-Path $root 'dist\DriverX'
dotnet publish $project -c Release -r win-x64 --self-contained false -o $output
Write-Host "DriverX 已生成：$output\DriverX.exe"
