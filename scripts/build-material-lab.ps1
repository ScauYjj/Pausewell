param([switch]$SkipVerification)
$ErrorActionPreference='Stop'
$materialRoot=Split-Path -Parent $PSScriptRoot
$materialDotnet=Join-Path $materialRoot '.tools/dotnet/dotnet.exe'
if(-not(Test-Path -LiteralPath $materialDotnet)){$materialDotnet=(Get-Command dotnet -ErrorAction Stop).Source}
Push-Location -LiteralPath $materialRoot
try {
 & $materialDotnet publish 'design/material-lab/Pausewell.MaterialLab.csproj' -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o 'artifacts/Pausewell-material-lab' --nologo
 if($LASTEXITCODE -ne 0){throw 'Material lab publish failed.'}
 Copy-Item -LiteralPath 'design/material-lab/README.md' -Destination 'artifacts/Pausewell-material-lab/README.md' -Force
 if(-not $SkipVerification){
  $verificationStarted=[DateTimeOffset]::Now
  $verificationProcess=Start-Process -FilePath (Join-Path $materialRoot 'artifacts/Pausewell-material-lab/Pausewell.MaterialLab.exe') -ArgumentList @('--verify','--report-dir','artifacts/material-lab/release-verification') -WorkingDirectory $materialRoot -PassThru
  $verificationProcess.WaitForExit()
  $report=Get-Content -LiteralPath 'artifacts/material-lab/release-verification/material-test-results.json' -Raw -Encoding UTF8|ConvertFrom-Json
  if($verificationProcess.ExitCode -ne 0 -or -not $report.success -or [DateTimeOffset]::Parse($report.startedAt) -lt $verificationStarted){throw 'Published material lab verification failed. Read artifacts/material-lab/release-verification/material-test-results.json.'}
  Write-Output ('Published material verification passed: '+$report.checks.Count+' checks.')
 }
 Write-Output 'Ready: artifacts/Pausewell-material-lab/Pausewell.MaterialLab.exe'
} finally {Pop-Location}
