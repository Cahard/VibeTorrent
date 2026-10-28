[CmdletBinding()]
param(
    [switch]$SelfContained,
    [string]$Runtime = 'win-x64',
    [string]$Output = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$localDotnet = Join-Path $root '.dotnet\dotnet.exe'
$dotnet = if (Test-Path $localDotnet) {
    $localDotnet
} else {
    (Get-Command dotnet -ErrorAction Stop).Source
}

if ([string]::IsNullOrWhiteSpace($Output)) {
    $Output = Join-Path $root 'artifacts\publish\win-x64'
}

$project = Join-Path $root 'src\ZTorrent.App\ZTorrent.App.csproj'
$publishArgs = @(
    'publish', $project,
    '-c', 'Release',
    '-r', $Runtime,
    '-o', $Output,
    '--nologo',
    '-p:PublishReadyToRun=true',
    '-p:DebugType=None'
)

if ($SelfContained) {
    $publishArgs += @('--self-contained', 'true', '-p:PublishSingleFile=true')
} else {
    $publishArgs += @('--self-contained', 'false')
}

Write-Host "Publishing ZTorrent to $Output (self-contained=$SelfContained)" -ForegroundColor Cyan
& $dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

Write-Host 'Публикация готова.' -ForegroundColor Green
