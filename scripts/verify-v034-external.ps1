param()
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
Add-Type -TypeDefinition 'using System;using System.Runtime.InteropServices;public static class IntegrationNativeWindow{[DllImport("user32.dll")]public static extern bool IsWindowEnabled(IntPtr hwnd);}'
$integrationRoot=Split-Path -Parent $PSScriptRoot
$integrationExe=Join-Path $integrationRoot 'artifacts/Pausewell-ui-v034/Pausewell.exe'
$verificationData=Join-Path $integrationRoot ('artifacts/v034/restart-data/'+[guid]::NewGuid().ToString('N'))
$checks=[Collections.Generic.List[object]]::new()
$verificationProcess=$null
$success=$false
function Check-Integration([string]$name,[bool]$passed){$checks.Add([pscustomobject]@{name=$name;passed=$passed});if(-not $passed){throw "Failed: $name"}}
function Find-Integration($root,[string]$id){for($attempt=0;$attempt -lt 30;$attempt++){$element=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,$id));if($element){return $element};Start-Sleep -Milliseconds 100};throw "Missing control: $id"}
function Invoke-Integration($root,[string]$id){$element=Find-Integration $root $id;([Windows.Automation.InvokePattern]$element.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)).Invoke();Start-Sleep -Milliseconds 250}
function Set-Integration($root,[string]$id,[string]$value){$element=Find-Integration $root $id;([Windows.Automation.ValuePattern]$element.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern)).SetValue($value)}
function Start-Integration {
 $script:verificationProcess=Start-Process -FilePath $integrationExe -ArgumentList ('--data-dir "'+$verificationData+'"') -WorkingDirectory $integrationRoot -WindowStyle Normal -PassThru
 for($attempt=0;$attempt -lt 100;$attempt++){$verificationProcess.Refresh();if($verificationProcess.HasExited){throw 'Trial app exited during startup.'};if($verificationProcess.MainWindowHandle -ne [IntPtr]::Zero){$candidate=[Windows.Automation.AutomationElement]::FromHandle($verificationProcess.MainWindowHandle);if($candidate.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,'NavToday'))){return $candidate}};Start-Sleep -Milliseconds 150};throw 'No own main window.'
}
function Editor-Integration($root){for($attempt=0;$attempt -lt 30;$attempt++){$element=$root.FindFirst([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Window));if($element){return $element};Start-Sleep -Milliseconds 100};throw 'No editor window.'}
function Quit-Integration($root){Invoke-Integration $root 'NavSettings';Invoke-Integration $root 'GeneralSettings';Invoke-Integration $root 'QuitApp';if(-not $verificationProcess.WaitForExit(10000)){throw 'App failed to quit normally.'};Check-Integration 'Own process exits normally' ($verificationProcess.ExitCode -eq 0)}
try {
 $window=Start-Integration
 Check-Integration 'Published app opens fresh isolated database' ($window.Current.ProcessId -eq $verificationProcess.Id -and (Test-Path -LiteralPath (Join-Path $verificationData 'pausewell.db')))
 Invoke-Integration $window 'FirstWater';$editor=Editor-Integration $window
 Check-Integration 'Native editor blocks owner interaction' (-not [IntegrationNativeWindow]::IsWindowEnabled([IntPtr]$window.Current.NativeWindowHandle))
 Set-Integration $editor 'PlanName' 'Restart persistence check';Set-Integration $editor 'IntervalMinutes' '42';Invoke-Integration $editor 'SavePlan'
 Check-Integration 'Native save returns to main window' $window.Current.IsEnabled
 $firstPid=$verificationProcess.Id
 Quit-Integration $window
 $window=Start-Integration
 Check-Integration 'Restart uses a new process' ($verificationProcess.Id -ne $firstPid)
 $name=$window.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,'Restart persistence check'))
 Check-Integration 'Saved plan survives actual exit and restart' ($null -ne $name)
 $all=$window.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::CheckBox))
 $toggles=@($all|Where-Object {$_.Current.AutomationId.StartsWith('Enabled_')})
 Check-Integration 'Enabled state survives actual restart' ($toggles.Count -eq 1 -and ([Windows.Automation.TogglePattern]$toggles[0].GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern)).Current.ToggleState -eq [Windows.Automation.ToggleState]::On)
 Invoke-Integration $window ('Edit_'+$toggles[0].Current.AutomationId.Substring(8))
 $editor=Editor-Integration $window
 $interval=Find-Integration $editor 'IntervalMinutes'
 Check-Integration 'Edited interval survives actual restart' (([Windows.Automation.ValuePattern]$interval.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern)).Current.Value -eq '42')
 Invoke-Integration $editor 'CancelPlan'
 Quit-Integration $window
 $success=$true
} finally {
 if(-not $success -and $verificationProcess -and -not $verificationProcess.HasExited -and $verificationProcess.Path -eq $integrationExe){Stop-Process -InputObject $verificationProcess}
 [pscustomobject]@{success=$success;finishedAt=[DateTimeOffset]::Now;executable=$integrationExe;dataDirectory=$verificationData;checks=$checks;scope='External UIA against published EXE, native save, normal exit, actual process restart, isolated SQLite.'}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $integrationRoot 'artifacts/v034/external-uia-results.json') -Encoding UTF8
}
Write-Output ('External integration verification passed: '+$checks.Count+' checks.')
