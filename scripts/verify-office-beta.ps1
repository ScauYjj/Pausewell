param()
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSHOME 'Modules/Microsoft.PowerShell.Utility') -ErrorAction Stop
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
Add-Type -TypeDefinition 'using System;using System.Runtime.InteropServices;public static class OfficeBetaWindow{[StructLayout(LayoutKind.Sequential)]public struct POINT{public int X;public int Y;}[DllImport("user32.dll")]public static extern bool IsWindowVisible(IntPtr hwnd);[DllImport("user32.dll")]public static extern bool SetCursorPos(int x,int y);[DllImport("user32.dll")]public static extern bool GetCursorPos(out POINT point);[DllImport("user32.dll")]public static extern void mouse_event(uint flags,uint x,uint y,uint data,IntPtr extra);}'
$workspace=Split-Path -Parent $PSScriptRoot
$output=Join-Path $workspace 'artifacts/office-beta0310'
$testRoot=Join-Path $output ('verification/'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$checks=New-Object 'Collections.Generic.List[object]'
$started=[DateTimeOffset]::UtcNow
$app=$null;$success=$false
function Check([string]$name,[bool]$passed){$checks.Add([pscustomobject]@{name=$name;passed=$passed});Write-Output ((@('FAIL ','PASS ')[$passed])+$name);if(-not $passed){throw $name}}
function Collect([string]$data,[string]$directory){$text=& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Collect-OfficeTrial.ps1') -DataDirectory $data -OutputDirectory $directory -NoOpen;if($LASTEXITCODE -ne 0){throw 'Collector process failed'};return ($text|ConvertFrom-Json)}
function Find-Control($window,[string]$id){for($attempt=0;$attempt -lt 100;$attempt++){$element=$window.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,$id));if($element){return $element};Start-Sleep -Milliseconds 100};throw "Missing control: $id"}
function Invoke-Control($window,[string]$id){$element=Find-Control $window $id;([Windows.Automation.InvokePattern]$element.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)).Invoke();Start-Sleep -Milliseconds 300}
function Open-TestApp([string]$data){
 $script:app=Start-Process -FilePath $script:preflight.executable -ArgumentList ('--diagnostics --data-dir "'+$data+'"') -WindowStyle Normal -PassThru
 for($attempt=0;$attempt -lt 120;$attempt++){$app.Refresh();if($app.HasExited){throw 'Own trial app exited early'};if($app.MainWindowHandle -ne [IntPtr]::Zero){$window=[Windows.Automation.AutomationElement]::FromHandle($app.MainWindowHandle);if($window.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,'NavToday'))){return $window}};Start-Sleep -Milliseconds 100};throw 'Own installed window did not become ready'
}
function Quit-TestApp($window){Invoke-Control $window 'NavSettings';Invoke-Control $window 'QuitApp';if(-not $app.WaitForExit(15000) -or $app.ExitCode -ne 0){throw 'Own test app did not exit normally'};$app.Dispose();$script:app=$null}
function Return-FromTray {
 $desktop=[Windows.Automation.AutomationElement]::RootElement
 $tray=$desktop.FindFirst([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ClassNameProperty,'Shell_TrayWnd'))
 $condition=[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Button)
 $icons=@($tray.FindAll([Windows.Automation.TreeScope]::Descendants,$condition)|Where-Object {$_.Current.Name.StartsWith('间歇 ·')})
 $openWindows=$desktop.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.Condition]::TrueCondition)
 foreach($candidate in $openWindows){if($candidate.Current.ClassName -in @('NotifyIconOverflowWindow','TopLevelWindowForOverflowXamlIsland')){$icons+=@($candidate.FindAll([Windows.Automation.TreeScope]::Descendants,$condition)|Where-Object {$_.Current.Name.StartsWith('间歇 ·') -and -not $_.Current.IsOffscreen})}}
 if($icons.Count -eq 0){
  $buttons=$tray.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,'SystemTrayIcon'))
  $expand=@($buttons|Where-Object {$_.Current.Name -in @('显示隐藏的图标','Show hidden icons')})
  if($expand.Count -ne 1){throw 'Cannot uniquely locate tray overflow button'}
  ([Windows.Automation.InvokePattern]$expand[0].GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)).Invoke();Start-Sleep -Milliseconds 700
  $windows=$desktop.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.Condition]::TrueCondition)
  foreach($candidate in $windows){if($candidate.Current.ClassName -in @('NotifyIconOverflowWindow','TopLevelWindowForOverflowXamlIsland','Shell_TrayWnd')){$icons+=@($candidate.FindAll([Windows.Automation.TreeScope]::Descendants,$condition)|Where-Object {$_.Current.Name.StartsWith('间歇 ·')})}}
 }
 if($icons.Count -ne 1){throw 'Cannot uniquely identify our trial tray icon; no other icon will be clicked'}
 if($icons[0].Current.IsOffscreen){throw 'Our tray icon is offscreen; no click will be sent'}
 $bounds=$icons[0].Current.BoundingRectangle
 if($bounds.IsEmpty -or $bounds.Width -le 0 -or $bounds.Height -le 0){throw 'Our tray icon has no visible bounds'}
 $pointX=$bounds.X+$bounds.Width/2;$pointY=$bounds.Y+$bounds.Height/2;$cursor=New-Object OfficeBetaWindow+POINT
 [OfficeBetaWindow]::GetCursorPos([ref]$cursor)|Out-Null
 try{
  [OfficeBetaWindow]::SetCursorPos([int]$pointX,[int]$pointY)|Out-Null
  for($click=0;$click -lt 2;$click++){[OfficeBetaWindow]::mouse_event(2,0,0,0,[IntPtr]::Zero);[OfficeBetaWindow]::mouse_event(4,0,0,0,[IntPtr]::Zero);Start-Sleep -Milliseconds 80}
 }finally{[OfficeBetaWindow]::SetCursorPos($cursor.X,$cursor.Y)|Out-Null}
 for($attempt=0;$attempt -lt 100;$attempt++){$app.Refresh();if($app.MainWindowHandle -ne [IntPtr]::Zero){$restored=[Windows.Automation.AutomationElement]::FromHandle($app.MainWindowHandle);if($restored.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,'NavSettings'))){return $restored}};Start-Sleep -Milliseconds 100};throw 'Tray double-click did not restore the main window'
}
$normalDb=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'PausewellPrototype/pausewell.db'
$normalHash=if(Test-Path -LiteralPath $normalDb){(Get-FileHash -LiteralPath $normalDb).Hash}else{'absent'}
$registry=Get-ItemProperty -LiteralPath 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Uninstall/Pausewell' -ErrorAction SilentlyContinue
$registryBefore=@{location=$registry.InstallLocation;version=$registry.DisplayVersion;uninstall=$registry.UninstallString}|ConvertTo-Json -Compress
$runBefore=(Get-ItemProperty -LiteralPath 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Run' -Name Pausewell -ErrorAction SilentlyContinue).Pausewell
try{
 $preflightText=& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Start-OfficeTrial.ps1') -NoLaunch
 if($LASTEXITCODE -ne 0){throw 'Preflight failed'};$preflight=$preflightText|ConvertFrom-Json
 Check 'Installed desktop executable matches the previously verified release' ($preflight.source -eq 'installed' -and (Get-FileHash -LiteralPath $preflight.executable).Hash -eq $preflight.sha256)
 Check 'Current-user uninstall registration points to the installed v0.3.10 and a valid script' ($registry.InstallLocation -eq (Split-Path -Parent $preflight.executable) -and $registry.DisplayVersion -eq '0.3.10' -and (Test-Path -LiteralPath (Join-Path $registry.InstallLocation 'Manage-Pausewell.ps1')))
 $shortcutPath=Join-Path ([Environment]::GetFolderPath('Programs')) '间歇 Pausewell.lnk'
 $shortcut=(New-Object -ComObject WScript.Shell).CreateShortcut($shortcutPath)
 Check 'Installed Start menu shortcut resolves to the verified executable' ((Test-Path -LiteralPath $shortcutPath) -and $shortcut.TargetPath -eq $preflight.executable)
 $savedErrorAction=$ErrorActionPreference
 try{$ErrorActionPreference='Continue';$rejected=& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Start-OfficeTrial.ps1') -NoLaunch -DataDirectory (Split-Path -Parent $normalDb) 2>&1;$rejectedExit=$LASTEXITCODE}finally{$ErrorActionPreference=$savedErrorAction}
 Check 'Office trial refuses the real normal-data directory' ($rejectedExit -ne 0)
 $emptyData=Join-Path $testRoot 'empty'
 $empty=Collect $emptyData (Join-Path $testRoot 'empty-report');$emptyReport=Get-Content -LiteralPath $empty.report -Raw -Encoding UTF8|ConvertFrom-Json
 Check 'No logs produces an honest empty report without creating a database' ($emptyReport.eventCount -eq 0 -and -not (Test-Path -LiteralPath (Join-Path $emptyData 'pausewell.db')) -and -not $emptyReport.allDayAccepted)
 $fixtures=Join-Path $testRoot 'fixtures';$diagnostics=Join-Path $fixtures 'diagnostics';New-Item -ItemType Directory -Path $diagnostics -Force|Out-Null
 $privateMarker='SECRET_DRUG_NAME_DO_NOT_EXPORT';$runA='aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa';$runB='bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'
 function Event([string]$utc,[string]$run,[string]$name,$fields){return (@{utc=$utc;run=$run;eventName=$name;fields=$fields;privateText=$privateMarker}|ConvertTo-Json -Depth 8 -Compress)}
 $fixtureLines=@(
 (Event '2026-10-17T00:00:00Z' $runA 'start' @{privateName=$privateMarker}),
 (Event '2026-10-17T00:00:01Z' $runA 'sample' @{privateBytes=100MB;workingSetBytes=90MB;cpuSeconds=200}),
 (Event '2026-10-17T08:00:01Z' $runA 'sample' @{privateBytes=120MB;workingSetBytes=100MB;cpuSeconds=220}),
 (Event '2026-10-17T08:00:02Z' $runA 'session_lock' @{origin='verification';privateName=$privateMarker}),
 (Event '2026-10-17T08:00:03Z' $runA 'session_unlock' @{origin='os'}),
 (Event '2026-10-17T08:00:04Z' $runA 'reminder_shown' @{delaySeconds=3600;occurrence=$privateMarker;name=$privateMarker}),
 (Event '2026-10-17T08:00:05Z' $runA 'preview_shown' @{privateName=$privateMarker}),
 (Event '2026-10-17T08:00:06Z' $runA 'exit' @{normal=$true}),
 (Event '2026-10-17T08:30:00Z' $runB 'start' @{}),
 (Event '2026-10-17T08:30:01Z' $runB 'sample' @{privateBytes=80MB;workingSetBytes=70MB;cpuSeconds=2}),
 (Event '2026-10-17T08:31:01Z' $runB 'sample' @{privateBytes=81MB;workingSetBytes=70MB;cpuSeconds=3}),
 (Event '2026-10-17T08:31:02Z' $runB 'sample' @{privateBytes=$privateMarker;workingSetBytes=70MB;cpuSeconds=3}),
 '{broken-json',
 (Event '2026-10-17T08:31:03Z' $privateMarker 'scheduler_fault' @{message=$privateMarker})
 )
 [IO.File]::WriteAllLines((Join-Path $diagnostics 'runtime.jsonl'),$fixtureLines,(New-Object Text.UTF8Encoding($false)))
 $fixture=Collect $fixtures (Join-Path $testRoot 'fixture-report');$reportText=Get-Content -LiteralPath $fixture.report -Raw -Encoding UTF8;$report=$reportText|ConvertFrom-Json
 $markdown=Get-Content -LiteralPath (Join-Path $fixture.outputDirectory '办公试用记录.md') -Raw -Encoding UTF8
 Check 'Report excludes arbitrary fields, occurrence IDs, private text and malformed lines' (-not $reportText.Contains($privateMarker) -and -not $markdown.Contains($privateMarker) -and $report.invalidLines -eq 3)
 Check 'CPU deltas are calculated per process run without mixing resets' ($report.runs.Count -eq 2 -and $report.runs[0].sampledCpuDeltaSeconds -eq 20 -and $report.runs[1].sampledCpuDeltaSeconds -eq 1)
 Check 'Eight-hour sample gap is exposed rather than counted as continuous acceptance' ($report.runs[0].maxSampleGapSeconds -eq 28800 -and -not $report.allDayAccepted -and -not $report.realDeviceAccepted)
 Check 'Synthetic and OS events remain separate and preview is not a real reminder' ($report.syntheticSignals.session_lock -eq 1 -and $report.osSignals.session_lock -eq 0 -and $report.osSignals.session_unlock -eq 1 -and $report.eventCounts.reminder_shown -eq 1 -and $report.eventCounts.preview_shown -eq 1)
 Check 'Long due-to-display intervals are disclosed without being labelled scheduler latency' ($report.maxObservedDueToDisplaySeconds -eq 3600 -and $markdown.Contains('不等于程序调度延迟'))
 Check 'Running or rotated log segments are not labelled as crashes' ($report.runs[0].normalExitSeen -and -not $report.runs[1].normalExitSeen -and $markdown.Contains('程序仍在运行'))
 $oversizePath=Join-Path $diagnostics 'runtime.previous.jsonl';$oversize=[IO.File]::Create($oversizePath);try{$oversize.SetLength(2MB+1)}finally{$oversize.Dispose()}
 $oversizeResult=Collect $fixtures (Join-Path $testRoot 'oversize-report');$oversizeReport=Get-Content -LiteralPath $oversizeResult.report -Raw -Encoding UTF8|ConvertFrom-Json
 Check 'Oversized unexpected log files are skipped with coverage disclosed' ($oversizeReport.oversizedFilesSkipped -eq 1)
 $liveData=Join-Path $testRoot 'native-data';$window=Open-TestApp $liveData
 Check 'Installed executable opens an empty office trial as a native window' ($window.Current.Name.Contains('0.3.10'))
 $runningText=& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Start-OfficeTrial.ps1') -DataDirectory $liveData -NoLaunch
 $running=$runningText|ConvertFrom-Json
 Check 'Preflight detects the same data-directory instance' $running.alreadyRunning
 $duplicateMessage=& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Start-OfficeTrial.ps1') -DataDirectory $liveData
 Check 'Repeated start returns without launching a duplicate or showing the app mutex error' ($LASTEXITCODE -eq 0 -and ($duplicateMessage -join '').Contains('已经在运行'))
 $nativeHandle=[IntPtr]$window.Current.NativeWindowHandle
 ([Windows.Automation.WindowPattern]$window.GetCurrentPattern([Windows.Automation.WindowPattern]::Pattern)).Close();Start-Sleep -Seconds 2
 $app.Refresh()
 Check 'Closing the main window leaves this test instance running in the background' (-not $app.HasExited -and -not [OfficeBetaWindow]::IsWindowVisible($nativeHandle))
 $restoredWindow=Return-FromTray
 Check 'Double-clicking the unique real tray icon restores the hidden main window' ($null -ne $restoredWindow)
 Quit-TestApp $restoredWindow
 Check 'Tray-returned instance can enter settings and exit normally' ($null -eq $app)
 $window=Open-TestApp $liveData;Quit-TestApp $window
 Check 'Installed executable reopens the same trial directory and exits normally again' ($null -eq $app)
 $live=Collect $liveData (Join-Path $testRoot 'native-report');$liveReport=Get-Content -LiteralPath $live.report -Raw -Encoding UTF8|ConvertFrom-Json
 Check 'Collector sees two real start and normal-exit cycles' ($liveReport.eventCounts.start -eq 2 -and $liveReport.eventCounts.exit -eq 2 -and $liveReport.runs.Count -eq 2 -and $liveReport.eventCounts.scheduler_fault -eq 0)
 $registryAfter=Get-ItemProperty -LiteralPath 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Uninstall/Pausewell'
 $registryAfterJson=@{location=$registryAfter.InstallLocation;version=$registryAfter.DisplayVersion;uninstall=$registryAfter.UninstallString}|ConvertTo-Json -Compress
 $runAfter=(Get-ItemProperty -LiteralPath 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Run' -Name Pausewell -ErrorAction SilentlyContinue).Pausewell
 $normalHashAfter=if(Test-Path -LiteralPath $normalDb){(Get-FileHash -LiteralPath $normalDb).Hash}else{'absent'}
 Check 'Normal database bytes, startup and installed registration are preserved' ($normalHash -eq $normalHashAfter -and $registryBefore -eq $registryAfterJson -and $runBefore -eq $runAfter)
 $success=$true
}finally{
 if($app){$app.Refresh();if(-not $app.HasExited -and $app.Path -eq $preflight.executable){Stop-Process -InputObject $app};$app.Dispose()}
 [pscustomobject]@{success=$success;startedAt=$started.ToString('o');finishedAt=[DateTimeOffset]::UtcNow.ToString('o');testRoot=$testRoot;checks=$checks;installedAppSha256=$preflight.sha256;normalDataModified=$false;realLockTested=$false;realSleepTested=$false;trayDoubleClickTested=$success;allDayTested=$false}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $output 'verification.json') -Encoding UTF8
}
