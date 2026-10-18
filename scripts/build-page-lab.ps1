param([switch]$SkipVerification)
$ErrorActionPreference='Stop'
$pageRoot=Split-Path -Parent $PSScriptRoot
$pageDotnet=Join-Path $pageRoot '.tools/dotnet/dotnet.exe'
if(-not(Test-Path -LiteralPath $pageDotnet)){$pageDotnet=(Get-Command dotnet -ErrorAction Stop).Source}
Push-Location -LiteralPath $pageRoot
try {
 & $pageDotnet publish 'design/page-lab/Pausewell.PageLab.csproj' -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o 'artifacts/Pausewell-page-lab' --nologo
 if($LASTEXITCODE -ne 0){throw 'Page lab publish failed.'}
 Copy-Item -LiteralPath 'design/page-lab/README.md' -Destination 'artifacts/Pausewell-page-lab/README.md' -Force
 if(-not $SkipVerification){
  $verificationStarted=[DateTimeOffset]::Now
  $verificationProcess=Start-Process -FilePath (Join-Path $pageRoot 'artifacts/Pausewell-page-lab/Pausewell.PageLab.exe') -ArgumentList @('--verify','--report-dir','artifacts/page-lab/release-verification') -WorkingDirectory $pageRoot -PassThru
  $verificationProcess.WaitForExit()
  $report=Get-Content -LiteralPath 'artifacts/page-lab/release-verification/page-test-results.json' -Raw -Encoding UTF8|ConvertFrom-Json
  if($verificationProcess.ExitCode -ne 0 -or -not $report.success -or [DateTimeOffset]::Parse($report.startedAt) -lt $verificationStarted){throw 'Published page verification failed. Read artifacts/page-lab/release-verification/page-test-results.json.'}
  Write-Output ('Published baseline page verification passed: '+$report.checks.Count+' checks.')
 }
 Write-Output 'Ready: artifacts/Pausewell-page-lab/Pausewell.PageLab.exe'
} finally {Pop-Location}
