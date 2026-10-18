param()
$ErrorActionPreference='Stop'
$integrationRoot=Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $integrationRoot
try {
 & (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory 'artifacts/Pausewell-ui-v038'
 $verificationStarted=[DateTimeOffset]::Now
 $verificationProcess=Start-Process -FilePath (Join-Path $integrationRoot 'artifacts/Pausewell-ui-v038/Pausewell.exe') -ArgumentList @('--smoke','--report-dir','artifacts/v038/release-gui') -WorkingDirectory $integrationRoot -WindowStyle Normal -PassThru
 if(-not $verificationProcess.WaitForExit(240000)){throw 'Published verification exceeded 4 minutes. Inspect its own process and report.'}
 $report=Get-Content -LiteralPath 'artifacts/v038/release-gui/gui-test-results.json' -Raw -Encoding UTF8|ConvertFrom-Json
 if($verificationProcess.ExitCode -ne 0 -or -not $report.success -or [DateTimeOffset]::Parse($report.startedAt) -lt $verificationStarted){throw 'Published integration verification failed. Read artifacts/v038/release-gui/gui-test-results.json.'}
 Write-Output ('Published integration verification passed: '+$report.checks.Count+' checks.')
} finally {Pop-Location}
