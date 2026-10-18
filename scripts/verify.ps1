param([switch]$RulesOnly)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$dotnetPath = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnetPath)) { $dotnetPath = (Get-Command dotnet -ErrorAction Stop).Source }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
Push-Location -LiteralPath $projectRoot
try {
    & $dotnetPath run --project 'tests/Pausewell.Specs/Pausewell.Specs.csproj' -c Release -- 'artifacts/rules-test-results.json'
    if ($LASTEXITCODE -ne 0) { throw 'Rule verification failed.' }
    if ($RulesOnly) { return }
    & $dotnetPath build 'src/Pausewell.Desktop/Pausewell.Desktop.csproj' -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    $guiStartedAt = [DateTimeOffset]::UtcNow
    & $dotnetPath 'src/Pausewell.Desktop/bin/Release/net10.0-windows/Pausewell.dll' --smoke --report-dir 'artifacts/verification/gui'
    if ($LASTEXITCODE -ne 0) { throw 'GUI process reported failure.' }
    $resultPath = Join-Path $projectRoot 'artifacts/verification/gui/gui-test-results.json'
    if (-not (Test-Path -LiteralPath $resultPath)) { throw 'GUI verification did not produce a report.' }
    $result = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([DateTimeOffset]::Parse($result.startedAt) -lt $guiStartedAt) { throw 'GUI report is stale.' }
    if (-not $result.success) { throw 'GUI verification failed. Read artifacts/verification/gui/gui-test-results.json.' }
    Write-Output "GUI verification passed: $($result.checks.Count) checks."
} finally { Pop-Location }
