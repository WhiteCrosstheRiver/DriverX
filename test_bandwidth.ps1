param(
    [ValidatePattern('^[A-Za-z]$')][string]$Drive = 'K',
    [ValidateRange(8, 1024)][int]$SizeMB = 32
)
$ErrorActionPreference = 'Stop'
$name = '.__driverx_bandwidth_test_{0}.bin' -f ([guid]::NewGuid().ToString('N'))
$local = Join-Path $env:TEMP $name
$remote = ($Drive.ToUpperInvariant() + ':\' + $name)
$down = Join-Path $env:TEMP ('down_' + $name)
try {
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    $buf = New-Object byte[] (1MB)
    $fs = [IO.File]::OpenWrite($local)
    for ($i = 0; $i -lt $SizeMB; $i++) { $rng.GetBytes($buf); $fs.Write($buf, 0, $buf.Length) }
    $fs.Close()
    $up = [Diagnostics.Stopwatch]::StartNew(); Copy-Item -LiteralPath $local -Destination $remote; $up.Stop()
    $downWatch = [Diagnostics.Stopwatch]::StartNew(); Copy-Item -LiteralPath $remote -Destination $down; $downWatch.Stop()
    $h1 = (Get-FileHash $local -Algorithm SHA256).Hash
    $h2 = (Get-FileHash $down -Algorithm SHA256).Hash
    [pscustomobject]@{
        Drive = ($Drive.ToUpperInvariant() + ':')
        SizeMB = $SizeMB
        UploadSeconds = [math]::Round($up.Elapsed.TotalSeconds, 2)
        UploadMBps = [math]::Round($SizeMB / $up.Elapsed.TotalSeconds, 2)
        DownloadSeconds = [math]::Round($downWatch.Elapsed.TotalSeconds, 2)
        DownloadMBps = [math]::Round($SizeMB / $downWatch.Elapsed.TotalSeconds, 2)
        HashMatch = ($h1 -eq $h2)
        Remote = $remote
    } | Format-List
}
finally {
    Remove-Item -LiteralPath $down -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $local -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $remote -Force -ErrorAction SilentlyContinue
    if (Test-Path $remote) { Write-Error '远程测试文件删除失败' } else { 'Cleanup: PASS' }
}
