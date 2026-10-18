param([int]$DemoProcessId)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
$uiRoot=Split-Path -Parent $PSScriptRoot
$demoExe=Join-Path $uiRoot 'artifacts/Pausewell-ui-v02/Pausewell.exe'
$demoDirectory=Join-Path $uiRoot 'artifacts/ui-v02/demo-data'
$evidence=[System.Collections.Generic.List[object]]::new()
function Get-AppRoot([int]$processId) {
    for($attempt=0;$attempt -lt 50;$attempt++) {
        $target=Get-Process -Id $processId -ErrorAction Stop
        if($target.MainWindowHandle -ne 0){return [System.Windows.Automation.AutomationElement]::FromHandle($target.MainWindowHandle)}
        Start-Sleep -Milliseconds 100
    }
    throw 'No visible demo window.'
}
function Find-AppControl($root,[string]$automationId) {
    $condition=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$automationId)
    for($attempt=0;$attempt -lt 40;$attempt++) {
        $control=$root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)
        if($null -ne $control){return $control}
        Start-Sleep -Milliseconds 100
    }
    throw ('Missing control: '+$automationId)
}
function Invoke-AppControl($root,[string]$automationId) {
    $control=Find-AppControl $root $automationId
    $pattern=$control.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke();Start-Sleep -Milliseconds 180
}
function Start-IsolatedDemo {
    $started=Start-Process -FilePath $demoExe -ArgumentList @('--demo','--data-dir',('"'+$demoDirectory+'"')) -WorkingDirectory $uiRoot -PassThru
    return $started.Id
}
function Test-SelectedTheme($root,[string]$themeId) {
    Invoke-AppControl $root 'NavSettings';Invoke-AppControl $root 'AppearanceSettings'
    $control=Find-AppControl $root ('Theme_'+$themeId)
    $texts=$control.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
    $selected=@($texts | Where-Object {$_.Current.Name -eq ([string][char]0x5DF2+[char]0x9009+[char]0x62E9)}).Count -gt 0
    if(-not $selected){throw ('Saved theme not selected after restart: '+$themeId)}
    $evidence.Add([pscustomobject]@{check='Theme selected after actual process restart';theme=$themeId;passed=$true})
}
$target=Get-Process -Id $DemoProcessId
if($target.Path -ne $demoExe){throw 'Refusing to interact with a different executable.'}
$demoCommand=Get-CimInstance Win32_Process -Filter ('ProcessId='+$DemoProcessId)
if($demoCommand.CommandLine -notlike '*--demo*' -or $demoCommand.CommandLine -notlike ('*'+$demoDirectory+'*')){throw 'Expected isolated demo arguments.'}
$root=Get-AppRoot $DemoProcessId
foreach($nav in @('NavToday','NavMedicine','NavHistory','NavSettings')){$null=Find-AppControl $root $nav}
$evidence.Add([pscustomobject]@{check='Published app exposes four native navigation controls';passed=$true})
Invoke-AppControl $root 'NavSettings';Invoke-AppControl $root 'AppearanceSettings';Invoke-AppControl $root 'Theme_blue';Invoke-AppControl $root 'SaveSettings'
Invoke-AppControl $root 'GeneralSettings';Invoke-AppControl $root 'QuitApp'
if(-not $target.WaitForExit(5000)){throw 'Demo did not exit gracefully.'}
$DemoProcessId=Start-IsolatedDemo;$root=Get-AppRoot $DemoProcessId;Test-SelectedTheme $root 'blue'
Invoke-AppControl $root 'Theme_champagne';Invoke-AppControl $root 'SaveSettings';Invoke-AppControl $root 'GeneralSettings'
$target=Get-Process -Id $DemoProcessId;Invoke-AppControl $root 'QuitApp'
if(-not $target.WaitForExit(5000)){throw 'Demo did not exit gracefully.'}
$DemoProcessId=Start-IsolatedDemo;$root=Get-AppRoot $DemoProcessId;Test-SelectedTheme $root 'champagne';Invoke-AppControl $root 'CancelSettings';Invoke-AppControl $root 'NavToday'
[pscustomobject]@{success=$true;checkedAt=[DateTimeOffset]::Now;processId=$DemoProcessId;exe=$demoExe;dataDirectory=$demoDirectory;checks=$evidence;note='Only isolated demo processes were closed and reopened. Default theme restored; window left open.'}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $uiRoot 'artifacts/ui-v02/published-uia-results.json') -Encoding UTF8
Write-Output ('Published native UI verified; demo left open: '+$DemoProcessId)
