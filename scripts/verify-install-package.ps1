param()
$ErrorActionPreference='Stop'
$workspace=Split-Path -Parent $PSScriptRoot
$dotnet=Join-Path $workspace '.tools/dotnet/dotnet.exe'
$started=[DateTimeOffset]::UtcNow
Push-Location -LiteralPath $workspace
try {
 & $dotnet run --project tests/Pausewell.Deployment.Specs -c Release -- $workspace
 if($LASTEXITCODE -ne 0){throw 'Deployment verification failed.'}
 & $dotnet run --project tests/Pausewell.Specs -c Release -- artifacts/install-v0310/rules-test-results.json
 if($LASTEXITCODE -ne 0){throw 'Core verification failed.'}
 $report=Get-Content -LiteralPath artifacts/install-v0310/deployment-test-results.json -Raw -Encoding UTF8 | ConvertFrom-Json
 $archiveHash=(Get-FileHash -LiteralPath artifacts/install-v0310/Pausewell-0.3.10-win-x64-install.zip).Hash
 if(-not $report.success -or $report.archiveSha256 -ne $archiveHash -or [DateTimeOffset]::Parse($report.startedAt) -lt $started){throw 'Missing, stale or mismatched deployment evidence.'}
 Write-Output ('Verified archive: '+$report.checks.Count+' deployment checks.')
} finally {Pop-Location}
