param()
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
$componentRoot=Split-Path -Parent $PSScriptRoot
$componentExe=Join-Path $componentRoot 'artifacts/Pausewell-component-lab/Pausewell.ComponentLab.exe'
$componentProcess=Get-Process -Name 'Pausewell.ComponentLab'|Where-Object {$_.Path -eq $componentExe}|Select-Object -First 1
if(-not $componentProcess){throw 'Open the published component lab first.'}
$componentProcess.Refresh()
$window=[Windows.Automation.AutomationElement]::FromHandle($componentProcess.MainWindowHandle)
$results=[Collections.Generic.List[object]]::new()
function Find-Component([string]$id){$e=$window.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,$id));if($null -eq $e){throw "Missing UIA element: $id"};return $e}
function Check-Component([string]$name,[bool]$passed){$results.Add([pscustomobject]@{name=$name;passed=$passed});if(-not $passed){throw "Failed: $name"}}
function Select-ComponentTab([string]$id){$e=Find-Component $id;([Windows.Automation.SelectionItemPattern]$e.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern)).Select();Start-Sleep -Milliseconds 160}
$success=$false
try {
 Check-Component 'Published process exposed through external UIA' ($window.Current.ProcessId -eq $componentProcess.Id)
 Select-ComponentTab 'Tab0'
 $name=Find-Component 'PlanName';$value=[Windows.Automation.ValuePattern]$name.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern)
 Check-Component 'Editable field ValuePattern and accessible name' ($value.Current.Value -eq '喝水' -and $name.Current.Name -eq '提醒名称')
 $combo=Find-Component 'ReminderStyle';$expand=[Windows.Automation.ExpandCollapsePattern]$combo.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern)
 $expand.Expand();Start-Sleep -Milliseconds 150
 Check-Component 'External ExpandCollapse opens real dropdown' ($expand.Current.ExpandCollapseState -eq [Windows.Automation.ExpandCollapseState]::Expanded)
 $expand.Collapse()
 $check=Find-Component 'EnablePlan';$toggle=[Windows.Automation.TogglePattern]$check.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern);$previous=$toggle.Current.ToggleState;$toggle.Toggle()
 Check-Component 'External TogglePattern changes checkbox' ($toggle.Current.ToggleState -ne $previous);$toggle.Toggle()
 Select-ComponentTab 'Tab1'
 $date=Find-Component 'StartDate_Input';$dateValue=[Windows.Automation.ValuePattern]$date.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern)
 $dateValue.SetValue('2028/02/29');$validate=Find-Component 'ValidateDates';([Windows.Automation.InvokePattern]$validate.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)).Invoke();Start-Sleep -Milliseconds 160
 Check-Component 'External date input normalizes valid leap day' ($dateValue.Current.Value -eq '2028-02-29')
 $dateValue.SetValue((Get-Date -Format 'yyyy-MM-dd'));([Windows.Automation.InvokePattern]$validate.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)).Invoke()
 Select-ComponentTab 'Tab2'
 $scroll=Find-Component 'LongScroll';$scrollPattern=[Windows.Automation.ScrollPattern]$scroll.GetCurrentPattern([Windows.Automation.ScrollPattern]::Pattern)
 Check-Component 'External ScrollPattern reports vertical overflow' $scrollPattern.Current.VerticallyScrollable
 $scrollPattern.Scroll([Windows.Automation.ScrollAmount]::NoAmount,[Windows.Automation.ScrollAmount]::LargeIncrement)
 Check-Component 'External ScrollPattern moves content' ($scrollPattern.Current.VerticalScrollPercent -gt 0)
 $scrollPattern.SetScrollPercent(-1,0)
 Select-ComponentTab 'Tab0'
 $success=$true
} finally {
 [pscustomobject]@{success=$success;finishedAt=[DateTimeOffset]::Now;processId=$componentProcess.Id;checks=$results;scope='External UIA against standalone native sample. No production app/data access.'}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $componentRoot 'artifacts/component-lab/external-uia-results.json') -Encoding UTF8
}
Write-Output ('External UIA passed: '+$results.Count+' checks.')
