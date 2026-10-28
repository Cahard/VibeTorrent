[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$localDotnet = Join-Path $root '.dotnet\dotnet.exe'
$dotnet = if (Test-Path $localDotnet) {
    $localDotnet
} else {
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        throw 'Не найден .NET SDK. Установите .NET 10 SDK или положите SDK в .dotnet.'
    }

    $command.Source
}

Write-Host "ZTorrent build ($Configuration)" -ForegroundColor Cyan
Write-Host "dotnet: $dotnet"

& $dotnet restore (Join-Path $root 'ZTorrent.slnx')
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }

& $dotnet build (Join-Path $root 'ZTorrent.slnx') -c $Configuration --no-restore --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

if (-not $SkipTests) {
    & $dotnet test (Join-Path $root 'tests\ZTorrent.Tests\ZTorrent.Tests.csproj') `
        -c $Configuration --no-build --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}

Write-Host 'Готово.' -ForegroundColor Green
