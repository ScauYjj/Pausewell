param()
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
$pageRoot=Split-Path -Parent $PSScriptRoot
$pageExe=Join-Path $pageRoot 'artifacts/Pausewell-page-lab/Pausewell.PageLab.exe'
$pageProcess=Get-Process -Name 'Pausewell.PageLab'|Where-Object {$_.Path -eq $pageExe}|Select-Object -First 1
if(-not $pageProcess){throw 'Open the published page lab first.'}
for($attempt=0;$attempt -lt 30;$attempt++){$pageProcess.Refresh();if($pageProcess.MainWindowHandle -ne [IntPtr]::Zero){break};Start-Sleep -Milliseconds 200};if($pageProcess.MainWindowHandle -eq [IntPtr]::Zero){throw 'Published page lab has no main window.'};$window=[Windows.Automation.AutomationElement]::FromHandle($pageProcess.MainWindowHandle)
$results=[Collections.Generic.List[object]]::new()
function Find-Page($root,[string]$id){for($attempt=0;$attempt -lt 20;$attempt++){$element=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,$id));if($element){return $element};Start-Sleep -Milliseconds 100};throw "Missing element: $id"}
function Check-Page([string]$name,[bool]$passed){$results.Add([pscustomobject]@{name=$name;passed=$passed});if(-not $passed){throw "Failed: $name"}}
function Invoke-Page($root,[string]$id){$element=Find-Page $root $id;([Windows.Automation.InvokePattern]$element.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)).Invoke();Start-Sleep -Milliseconds 200}
function Value-Page($root,[string]$id){$element=Find-Page $root $id;return [Windows.Automation.ValuePattern]$element.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern)}
function Editor-Page {for($attempt=0;$attempt -lt 20;$attempt++){$candidate=$window.FindFirst([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Window));if($candidate){return $candidate};Start-Sleep -Milliseconds 100};throw 'Missing editor window.' }
function Count-Page($root,[int]$count){$element=Find-Page $root ('DrugCount'+$count);([Windows.Automation.SelectionItemPattern]$element.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern)).Select();Start-Sleep -Milliseconds 150}
$success=$false
try {
 Check-Page 'Published baseline process exposed through UIA' ($window.Current.ProcessId -eq $pageProcess.Id)
 Invoke-Page $window 'PageAdd';$editor=Editor-Page
 Check-Page 'Editor isolates owner interaction' (-not $window.Current.IsEnabled)
 (Value-Page $editor 'FormName').SetValue('外部校验提醒');(Value-Page $editor 'FormInterval').SetValue('42');Invoke-Page $editor 'FormSave'
 Check-Page 'Ordinary save returns to owner and updates feedback' ($window.Current.IsEnabled -and (Find-Page $window 'PageFeedback').Current.Name.Contains('外部校验提醒'))
 Invoke-Page $window 'PageAdd';$editor=Editor-Page;Invoke-Page $editor 'FormCancel'
 Check-Page 'Unchanged new form cancels directly' $window.Current.IsEnabled
 Invoke-Page $window 'PageNav1';Invoke-Page $window 'PageAdd';$editor=Editor-Page
 Check-Page 'New medication times start empty' ((Value-Page $editor 'DrugTime0').Current.Value -eq '' -and (Value-Page $editor 'DrugTime1').Current.Value -eq '')
 (Value-Page $editor 'FormName').SetValue('外部校验药物');(Value-Page $editor 'DrugTime0').SetValue('10:15');(Value-Page $editor 'DrugTime1').SetValue('20:15')
 Count-Page $editor 3;(Value-Page $editor 'DrugTime2').SetValue('13:15');Count-Page $editor 1;Count-Page $editor 3
 Check-Page 'Native count selection restores hidden time draft' ((Value-Page $editor 'DrugTime2').Current.Value -eq '13:15')
 Invoke-Page $editor 'FormSave'
 Check-Page 'Medication save returns and updates feedback' ($window.Current.IsEnabled -and (Find-Page $window 'PageFeedback').Current.Name.Contains('外部校验药物'))
 Invoke-Page $window 'PageNav0'
 $success=$true
} finally {
 [pscustomobject]@{success=$success;finishedAt=[DateTimeOffset]::Now;processId=$pageProcess.Id;checks=$results;scope='External UIA against baseline pages in memory only.'}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $pageRoot 'artifacts/page-lab/external-uia-results.json') -Encoding UTF8
}
Write-Output ('External baseline page UIA passed: '+$results.Count+' checks.')
