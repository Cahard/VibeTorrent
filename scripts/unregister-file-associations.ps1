[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$classes = 'HKCU:\Software\Classes'

foreach ($key in @(
    "$classes\.torrent",
    "$classes\ZTorrent.Torrent",
    "$classes\magnet"
)) {
    if (Test-Path $key) {
        Remove-Item -Path $key -Recurse -Force
    }
}

Write-Host 'Ассоциации ZTorrent удалены.' -ForegroundColor Green
