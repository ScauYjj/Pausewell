param([string]$OutputDirectory = 'artifacts/publish/win-x64')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$dotnetPath = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnetPath)) {
    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $dotnetCommand) { throw 'Install .NET 10 SDK or prepare .tools/dotnet first.' }
    $dotnetPath = $dotnetCommand.Source
}
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
Push-Location -LiteralPath $projectRoot
try {
    & $dotnetPath run --project 'tests/Pausewell.Specs/Pausewell.Specs.csproj' -c Release -- 'artifacts/rules-test-results.json'
    if ($LASTEXITCODE -ne 0) { throw 'Rule verification failed.' }
    & $dotnetPath publish 'src/Pausewell.Desktop/Pausewell.Desktop.csproj' -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $OutputDirectory --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item -LiteralPath 'README.md' -Destination (Join-Path $OutputDirectory 'README.md') -Force
    Write-Output ('Ready: '+(Join-Path $OutputDirectory 'Pausewell.exe'))
} finally { Pop-Location }
