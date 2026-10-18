param()
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
$materialRoot=Split-Path -Parent $PSScriptRoot
$materialExe=Join-Path $materialRoot 'artifacts/Pausewell-material-lab/Pausewell.MaterialLab.exe'
$materialProcess=Get-Process -Name 'Pausewell.MaterialLab'|Where-Object {$_.Path -eq $materialExe}|Select-Object -First 1
if(-not $materialProcess){throw 'Open the published material lab first.'}
$materialProcess.Refresh();$window=[Windows.Automation.AutomationElement]::FromHandle($materialProcess.MainWindowHandle)
$results=[Collections.Generic.List[object]]::new()
function Find-Material([string]$id){$element=$window.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,$id));if(-not $element){throw "Missing element: $id"};return $element}
function Check-Material([string]$name,[bool]$passed){$results.Add([pscustomobject]@{name=$name;passed=$passed});if(-not $passed){throw "Failed: $name"}}
function Invoke-Material([string]$id){$element=Find-Material $id;([Windows.Automation.InvokePattern]$element.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)).Invoke();Start-Sleep -Milliseconds 220}
$success=$false
try {
 Check-Material 'Target is standalone published material process' ($window.Current.ProcessId -eq $materialProcess.Id)
 $slider=Find-Material 'LightChoice';$range=[Windows.Automation.RangeValuePattern]$slider.GetCurrentPattern([Windows.Automation.RangeValuePattern]::Pattern);$range.SetValue(.8)
 Check-Material 'Light slider exposes native RangeValuePattern' ([Math]::Abs($range.Current.Value-.8) -lt .01);$range.SetValue(.36)
 $reduced=Find-Material 'ReducedChoice';$toggle=[Windows.Automation.TogglePattern]$reduced.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern);$previous=$toggle.Current.ToggleState;$toggle.Toggle()
 Check-Material 'Reduced transparency exposes native TogglePattern' ($toggle.Current.ToggleState -ne $previous);$toggle.Toggle()
 Invoke-Material 'PreviewReminder';Start-Sleep -Milliseconds 450
 Check-Material 'Modal disables environment controls' (-not (Find-Material 'EnvironmentChoice').Current.IsEnabled)
 Check-Material 'Task primary action is visible and enabled' ((Find-Material 'ConfirmPreview').Current.IsEnabled -and -not (Find-Material 'ConfirmPreview').Current.IsOffscreen)
 Invoke-Material 'ConfirmPreview'
 Check-Material 'Confirm returns to enabled environment controls' (Find-Material 'EnvironmentChoice').Current.IsEnabled
 Invoke-Material 'CompareMaterial'
 Check-Material 'Independent transmission preview exposes return action' (-not (Find-Material 'ReturnPreview').Current.IsOffscreen)
 Invoke-Material 'ReturnPreview'
 Check-Material 'Return restores preview entry' (Find-Material 'PreviewReminder').Current.IsEnabled
 $success=$true
} finally {
 [pscustomobject]@{success=$success;finishedAt=[DateTimeOffset]::Now;processId=$materialProcess.Id;checks=$results;scope='External UIA against standalone material sample only.'}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $materialRoot 'artifacts/material-lab/external-uia-results.json') -Encoding UTF8
}
Write-Output ('External material UIA passed: '+$results.Count+' checks.')
