param(
    [string]$Remote = 'driverx-2037153313',
    [ValidateRange(8, 1024)][int]$SizeMB = 64
)
$ErrorActionPreference = 'Stop'
$rclone = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Links\rclone.exe'
$tag = [guid]::NewGuid().ToString('N')
$name = ".__driverx_wire_test_$tag.bin"
$local = Join-Path $env:TEMP $name
$down = Join-Path $env:TEMP "down_$name"
$remotePath = "$Remote`:/$name"
try {
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    $buf = New-Object byte[] (1MB)
    $fs = [IO.File]::OpenWrite($local)
    for ($i = 0; $i -lt $SizeMB; $i++) { $rng.GetBytes($buf); $fs.Write($buf, 0, $buf.Length) }
    $fs.Close()
    $up = [Diagnostics.Stopwatch]::StartNew()
    & $rclone copyto $local $remotePath --sftp-disable-hashcheck --transfers 1 --checkers 1 --stats 0
    if ($LASTEXITCODE) { throw "rclone upload failed: $LASTEXITCODE" }
    $up.Stop()
    $downWatch = [Diagnostics.Stopwatch]::StartNew()
    & $rclone copyto $remotePath $down --sftp-disable-hashcheck --transfers 1 --checkers 1 --stats 0
    if ($LASTEXITCODE) { throw "rclone download failed: $LASTEXITCODE" }
    $downWatch.Stop()
    [pscustomobject]@{
        Remote = $Remote; SizeMB = $SizeMB
        UploadSeconds = [math]::Round($up.Elapsed.TotalSeconds, 2)
        UploadMBps = [math]::Round($SizeMB / $up.Elapsed.TotalSeconds, 2)
        DownloadSeconds = [math]::Round($downWatch.Elapsed.TotalSeconds, 2)
        DownloadMBps = [math]::Round($SizeMB / $downWatch.Elapsed.TotalSeconds, 2)
        HashMatch = ((Get-FileHash $local -Algorithm SHA256).Hash -eq (Get-FileHash $down -Algorithm SHA256).Hash)
    } | Format-List
} finally {
    & $rclone deletefile $remotePath --stats 0 2>$null
    Remove-Item -LiteralPath $down,$local -Force -ErrorAction SilentlyContinue
    [pscustomobject]@{ CleanupRemote = (& $rclone lsf $remotePath --files-only --stats 0 2>$null | Measure-Object).Count -eq 0; CleanupLocal = (-not (Test-Path $local)) } | Format-List
}
