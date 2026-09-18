param([string]$Version='0.1.0')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$release=Join-Path $root "release\DriverX-$Version"
$portable=Join-Path $release 'DriverX-Portable'
$online=Join-Path $release 'DriverX-Online'
$offline=Join-Path $release 'DriverX-Offline'
New-Item -ItemType Directory -Force -Path $portable,$online,$offline | Out-Null
$publish=Join-Path $root 'release\_publish'
dotnet publish (Join-Path $root 'native\DriverX.Desktop\DriverX.Desktop.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $publish
Copy-Item (Join-Path $publish 'DriverX.exe') (Join-Path $portable 'DriverX.exe') -Force
Copy-Item (Join-Path $publish 'DriverX.exe') (Join-Path $online 'DriverX.exe') -Force
Copy-Item (Join-Path $publish 'DriverX.exe') (Join-Path $offline 'DriverX.exe') -Force
Copy-Item (Join-Path $PSScriptRoot 'Install-DriverX-Online.ps1') $online -Force
Copy-Item (Join-Path $PSScriptRoot 'Install-DriverX-Offline.ps1') $offline -Force
$url='https://github.com/winfsp/winfsp/releases/download/v2.1/winfsp-2.1.25156.msi'
Invoke-WebRequest $url -OutFile (Join-Path $offline 'winfsp-2.1.25156.msi')
Write-Host "Created: $release"
