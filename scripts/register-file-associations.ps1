[CmdletBinding()]
param(
    [string]$ExePath = ''
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ExePath)) {
    $candidates = @(
        (Join-Path $PSScriptRoot '..\artifacts\publish\win-x64\VibeTorrent.exe'),
        (Join-Path $PSScriptRoot '..\src\VibeTorrent.App\bin\Release\net10.0-windows\VibeTorrent.exe'),
        (Join-Path $PSScriptRoot '..\src\VibeTorrent.App\bin\Debug\net10.0-windows\VibeTorrent.exe')
    )
    $ExePath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($ExePath) -or -not (Test-Path $ExePath)) {
    throw 'Не найден VibeTorrent.exe. Укажите -ExePath.'
}

$ExePath = (Resolve-Path $ExePath).Path
$classes = 'HKCU:\Software\Classes'
$torrentProgId = 'VibeTorrent.Torrent'

New-Item -Path "$classes\.torrent" -Force | Out-Null
Set-ItemProperty -Path "$classes\.torrent" -Name '(default)' -Value $torrentProgId
Set-ItemProperty -Path "$classes\.torrent" -Name 'Content Type' -Value 'application/x-bittorrent'

New-Item -Path "$classes\$torrentProgId\DefaultIcon" -Force | Out-Null
Set-ItemProperty -Path "$classes\$torrentProgId\DefaultIcon" -Name '(default)' -Value "`"$ExePath`",0"

New-Item -Path "$classes\$torrentProgId\shell\open\command" -Force | Out-Null
Set-ItemProperty -Path "$classes\$torrentProgId\shell\open\command" -Name '(default)' -Value "`"$ExePath`" `"%1`""

New-Item -Path "$classes\magnet" -Force | Out-Null
Set-ItemProperty -Path "$classes\magnet" -Name '(default)' -Value 'URL:Magnet link'
Set-ItemProperty -Path "$classes\magnet" -Name 'URL Protocol' -Value ''
New-Item -Path "$classes\magnet\DefaultIcon" -Force | Out-Null
Set-ItemProperty -Path "$classes\magnet\DefaultIcon" -Name '(default)' -Value "`"$ExePath`",0"
New-Item -Path "$classes\magnet\shell\open\command" -Force | Out-Null
Set-ItemProperty -Path "$classes\magnet\shell\open\command" -Name '(default)' -Value "`"$ExePath`" `"%1`""

Write-Host "Ассоциации .torrent и magnet зарегистрированы для $ExePath" -ForegroundColor Green
Write-Host 'Проверьте «Открыть с помощью» в проводнике Windows.'
