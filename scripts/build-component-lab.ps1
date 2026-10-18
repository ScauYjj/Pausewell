param([switch]$SkipVerification)
$ErrorActionPreference='Stop'
$componentRoot=Split-Path -Parent $PSScriptRoot
$componentDotnet=Join-Path $componentRoot '.tools/dotnet/dotnet.exe'
if(-not(Test-Path -LiteralPath $componentDotnet)){$componentDotnet=(Get-Command dotnet -ErrorAction Stop).Source}
Push-Location -LiteralPath $componentRoot
try {
 & $componentDotnet publish 'design/component-lab/Pausewell.ComponentLab.csproj' -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o 'artifacts/Pausewell-component-lab' --nologo
 if($LASTEXITCODE -ne 0){throw 'Component lab publish failed.'}
 Copy-Item -LiteralPath 'design/component-lab/README.md' -Destination 'artifacts/Pausewell-component-lab/README.md' -Force
 if(-not $SkipVerification){
  $verificationStarted=[DateTimeOffset]::Now
  $verificationProcess=Start-Process -FilePath (Join-Path $componentRoot 'artifacts/Pausewell-component-lab/Pausewell.ComponentLab.exe') -ArgumentList @('--verify','--report-dir','artifacts/component-lab/release-verification') -WorkingDirectory $componentRoot -PassThru
  $verificationProcess.WaitForExit()
  $report=Get-Content -LiteralPath 'artifacts/component-lab/release-verification/native-test-results.json' -Raw -Encoding UTF8|ConvertFrom-Json
  if($verificationProcess.ExitCode -ne 0 -or -not $report.success -or [DateTimeOffset]::Parse($report.startedAt) -lt $verificationStarted){throw 'Published component lab verification failed. Read artifacts/component-lab/release-verification/native-test-results.json.'}
  Write-Output ('Published native verification passed: '+$report.checks.Count+' checks.')
 }
 Write-Output 'Ready: artifacts/Pausewell-component-lab/Pausewell.ComponentLab.exe'
} finally {Pop-Location}
