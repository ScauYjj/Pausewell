param()
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
Add-Type -TypeDefinition 'using System;using System.Runtime.InteropServices;public static class IntegrationNativeWindow{[DllImport("user32.dll")]public static extern bool IsWindowEnabled(IntPtr hwnd);}'
$integrationRoot=Split-Path -Parent $PSScriptRoot
$integrationExe=Join-Path $integrationRoot 'artifacts/Pausewell-ui-v039/Pausewell.exe'
$verificationData=Join-Path $integrationRoot ('artifacts/v039/restart-data/'+[guid]::NewGuid().ToString('N'))
$checks=[Collections.Generic.List[object]]::new()
$verificationProcess=$null
$success=$false
$verificationStarted=[DateTimeOffset]::UtcNow.ToString('o')
function Check-Integration([string]$name,[bool]$passed){$checks.Add([pscustomobject]@{name=$name;passed=$passed});if(-not $passed){throw "Failed: $name"}}
function Find-Integration($root,[string]$id){for($attempt=0;$attempt -lt 80;$attempt++){$element=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,$id));if($element){return $element};Start-Sleep -Milliseconds 100};throw "Missing control: $id"}
function Invoke-Integration($root,[string]$id){$element=Find-Integration $root $id;([Windows.Automation.InvokePattern]$element.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)).Invoke();Start-Sleep -Milliseconds 250}
function Set-Integration($root,[string]$id,[string]$value){$element=Find-Integration $root $id;([Windows.Automation.ValuePattern]$element.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern)).SetValue($value)}
function Toggle-Integration($root,[string]$id){$element=Find-Integration $root $id;([Windows.Automation.TogglePattern]$element.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern)).Toggle();Start-Sleep -Milliseconds 250}
function Select-Integration($root,[string]$id,[string]$label){$combo=Find-Integration $root $id;$expand=[Windows.Automation.ExpandCollapsePattern]$combo.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern);$expand.Expand();Start-Sleep -Milliseconds 150;$condition=[Windows.Automation.AndCondition]::new([Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,$label),[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::ListItem));$item=$combo.FindFirst([Windows.Automation.TreeScope]::Descendants,$condition);if(-not $item){throw "Missing option: $label"};([Windows.Automation.SelectionItemPattern]$item.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern)).Select();$expand.Collapse();Start-Sleep -Milliseconds 150}
function Wait-ReminderIntegration([string]$id){$deadline=[DateTimeOffset]::UtcNow.AddSeconds(90);while([DateTimeOffset]::UtcNow -lt $deadline){$windows=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$verificationProcess.Id));foreach($candidate in $windows){$button=$candidate.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,$id));if($button){return $candidate}};Start-Sleep -Milliseconds 200};throw 'Real-clock reminder was not shown.'}
function Has-IntegrationText($root,[string]$text){$labels=$root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Text));foreach($label in $labels){if($label.Current.Name.Contains($text)){return $true}};return $false}
function Start-Integration {
 $script:verificationProcess=Start-Process -FilePath $integrationExe -ArgumentList ('--data-dir "'+$verificationData+'"') -WorkingDirectory $integrationRoot -WindowStyle Normal -PassThru
 for($attempt=0;$attempt -lt 100;$attempt++){$verificationProcess.Refresh();if($verificationProcess.HasExited){throw 'Trial app exited during startup.'};if($verificationProcess.MainWindowHandle -ne [IntPtr]::Zero){$candidate=[Windows.Automation.AutomationElement]::FromHandle($verificationProcess.MainWindowHandle);if($candidate.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,'NavToday'))){return $candidate}};Start-Sleep -Milliseconds 150};throw 'No own main window.'
}
function Editor-Integration($root){for($attempt=0;$attempt -lt 80;$attempt++){$windows=$root.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Window));foreach($element in $windows){$nameInput=$element.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,'PlanName'));if($nameInput){return $element}};Start-Sleep -Milliseconds 100};throw 'No ready editor window.'}
function Task-Integration($root){for($attempt=0;$attempt -lt 80;$attempt++){$windows=$root.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Window));foreach($candidate in $windows){$field=$candidate.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,'ModeMinutes'));if($field){return $candidate}};Start-Sleep -Milliseconds 100};throw 'No ready mode window.'}
function Quit-Integration($root){Invoke-Integration $root 'NavSettings';Invoke-Integration $root 'GeneralSettings';Invoke-Integration $root 'QuitApp';if(-not $verificationProcess.WaitForExit(10000)){throw 'App failed to quit normally.'};Check-Integration 'Own process exits normally' ($verificationProcess.ExitCode -eq 0)}
try {
 $window=Start-Integration
 Check-Integration 'Published app opens fresh isolated database' ($window.Current.ProcessId -eq $verificationProcess.Id -and (Test-Path -LiteralPath (Join-Path $verificationData 'pausewell.db')))
 Invoke-Integration $window 'FirstWater';$editor=Editor-Integration $window
 Check-Integration 'Native editor blocks owner interaction' (-not [IntegrationNativeWindow]::IsWindowEnabled([IntPtr]$window.Current.NativeWindowHandle))
 Set-Integration $editor 'PlanName' 'Restart persistence check';Set-Integration $editor 'IntervalMinutes' '42';Invoke-Integration $editor 'SavePlan'
 Check-Integration 'Native save returns to main window' $window.Current.IsEnabled
 $enabled=$window.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::CheckBox));$waterToggleId=@($enabled|Where-Object {$_.Current.AutomationId.StartsWith('Enabled_')})[0].Current.AutomationId
 Toggle-Integration $window $waterToggleId;Invoke-Integration $window 'NavMedicine';Invoke-Integration $window 'NavToday';$waterToggle=Find-Integration $window $waterToggleId
 Check-Integration 'UIA disabling a reminder survives page navigation' (([Windows.Automation.TogglePattern]$waterToggle.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern)).Current.ToggleState -eq [Windows.Automation.ToggleState]::Off)
 Toggle-Integration $window $waterToggleId
 Invoke-Integration $window 'NavSettings';Invoke-Integration $window 'AppearanceSettings';Invoke-Integration $window 'Theme_violet';Invoke-Integration $window 'SaveSettings'
 Invoke-Integration $window 'GeneralSettings'
 $sound=Find-Integration $window 'SoundSetting';$soundPattern=[Windows.Automation.TogglePattern]$sound.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern);$savedSound=$soundPattern.Current.ToggleState;$soundPattern.Toggle()
 Invoke-Integration $window 'AppearanceSettings';Invoke-Integration $window 'Theme_rose';Invoke-Integration $window 'GeneralSettings'
 $draftSound=Find-Integration $window 'SoundSetting'
 Check-Integration 'General draft survives switching settings tabs' (([Windows.Automation.TogglePattern]$draftSound.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern)).Current.ToggleState -ne $savedSound)
 Invoke-Integration $window 'CancelSettings';$sound=Find-Integration $window 'SoundSetting';$soundPattern=[Windows.Automation.TogglePattern]$sound.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern)
 Check-Integration 'Cancel restores saved general preference' ($soundPattern.Current.ToggleState -eq $savedSound)
 Invoke-Integration $window 'AppearanceSettings';$themeChoice=Find-Integration $window 'Theme_violet'
 Check-Integration 'Cancel restores saved appearance selection' ($null -ne $themeChoice.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,'已选择')))
 Invoke-Integration $window 'GeneralSettings';$sound=Find-Integration $window 'SoundSetting';([Windows.Automation.TogglePattern]$sound.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern)).Toggle();Invoke-Integration $window 'SaveSettings'
 Invoke-Integration $window 'NavHistory';Set-Integration $window 'HistoryFrom_Input' '2026-02-30';Invoke-Integration $window 'ApplyHistory';$date=Find-Integration $window 'HistoryFrom_Input'
 Check-Integration 'Invalid history date remains editable through UIA' (([Windows.Automation.ValuePattern]$date.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern)).Current.Value -eq '2026-02-30')
 $filterHint=Find-Integration $window 'HistoryFilterHint';Check-Integration 'Invalid range explains unchanged results' ($filterHint.Current.Name.Contains('列表保留'))
 Invoke-Integration $window 'ResetHistory';$date=Find-Integration $window 'HistoryFrom_Input'
 Check-Integration 'Recent seven days resets invalid date' (([Windows.Automation.ValuePattern]$date.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern)).Current.Value -match '^\d{4}-\d{2}-\d{2}$')
 Invoke-Integration $window 'NavToday';Invoke-Integration $window 'Meeting30';$taskWindow=Task-Integration $window
 Check-Integration 'Meeting task disables native owner' (-not [IntegrationNativeWindow]::IsWindowEnabled([IntPtr]$window.Current.NativeWindowHandle))
 Set-Integration $taskWindow 'ModeMinutes' '30';Invoke-Integration $taskWindow 'CancelMode'
 Check-Integration 'Cancel meeting returns to usable owner' ([IntegrationNativeWindow]::IsWindowEnabled([IntPtr]$window.Current.NativeWindowHandle))
 Invoke-Integration $window 'PauseOrdinary';$taskWindow=Task-Integration $window
 $scope=Find-Integration $taskWindow 'ModeScope';$scopePattern=[Windows.Automation.TogglePattern]$scope.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern);$scopePattern.Toggle();Start-Sleep -Milliseconds 150
 Check-Integration 'Pause medication scope is operable through UIA' ($scopePattern.Current.ToggleState -eq [Windows.Automation.ToggleState]::On)
 Invoke-Integration $taskWindow 'CancelMode'
 Invoke-Integration $window 'NewPlan';$editor=Editor-Integration $window
 Select-Integration $editor 'FormKind' '自定义';Select-Integration $editor 'FormSchedule' '仅一次';Set-Integration $editor 'PlanName' 'Office flow once';Set-Integration $editor 'CustomContent' 'Submit the signed page after reviewing it.'
 $candidateTime=[DateTimeOffset]::UtcNow.AddSeconds(12).ToOffset([TimeSpan]::FromHours(8));$flowDue=[DateTimeOffset]::new($candidateTime.Year,$candidateTime.Month,$candidateTime.Day,$candidateTime.Hour,$candidateTime.Minute,0,$candidateTime.Offset).AddMinutes(1)
 Set-Integration $editor 'OnceDate_Input' $flowDue.ToString('yyyy-MM-dd');Set-Integration $editor 'OnceTime' $flowDue.ToString('HH:mm');Invoke-Integration $editor 'SavePlan'
 Check-Integration 'Custom one-time reminder is created through visible controls' (Has-IntegrationText $window 'Office flow once')
 $reminder=Wait-ReminderIntegration 'Complete_Other'
 Check-Integration 'Real clock shows the saved custom reminder content' (Has-IntegrationText $reminder 'Submit the signed page after reviewing it.')
 Invoke-Integration $reminder 'Complete_Other';Invoke-Integration $window 'NavHistory'
 $recordButtons=$window.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Button));$flowRecords=@($recordButtons|Where-Object {$_.Current.AutomationId.StartsWith('Record_')})
 Check-Integration 'Confirmation creates exactly one visible history record' ($flowRecords.Count -eq 1)
 $flowRecordId=$flowRecords[0].Current.AutomationId;Invoke-Integration $window $flowRecordId;Invoke-Integration $window 'CorrectRecord';Set-Integration $window 'CorrectionReason' 'Office flow correction';Invoke-Integration $window 'SaveCorrection'
 Check-Integration 'Record correction displays the saved reason and history' (Has-IntegrationText $window '原因：Office flow correction')
 Invoke-Integration $window 'CloseRecord';Invoke-Integration $window 'NavToday'
 $flowToggle=$window.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,'Office flow once启用状态'));$flowToggleId=$flowToggle.Current.AutomationId
 Check-Integration 'Completed one-time reminder ends automatically' (([Windows.Automation.TogglePattern]$flowToggle.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern)).Current.ToggleState -eq [Windows.Automation.ToggleState]::Off)
 Toggle-Integration $window $flowToggleId;$flowToggle=Find-Integration $window $flowToggleId
 Check-Integration 'Ended one-time reminder cannot be silently restarted' (([Windows.Automation.TogglePattern]$flowToggle.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern)).Current.ToggleState -eq [Windows.Automation.ToggleState]::Off)
 Invoke-Integration $window 'Meeting30';$taskWindow=Task-Integration $window;Set-Integration $taskWindow 'ModeMinutes' '15';Invoke-Integration $taskWindow 'ApplyMode'
 Check-Integration 'Applying a meeting updates the running page' (Has-IntegrationText $window '会议至')
 Invoke-Integration $window 'EndMode';Invoke-Integration $window 'PauseOrdinary';$taskWindow=Task-Integration $window;Toggle-Integration $taskWindow 'ModeScope'
 Check-Integration 'Accessible pause scope updates its explanatory text' (Has-IntegrationText $taskWindow '普通提醒和服药提示都暂停')
 Invoke-Integration $taskWindow 'ApplyMode';Check-Integration 'Applying pause retains its medication scope' (Has-IntegrationText $window '服药提示也暂停')
 Invoke-Integration $window 'EndMode'
 $firstPid=$verificationProcess.Id
 Quit-Integration $window
 $window=Start-Integration
 Check-Integration 'Restart uses a new process' ($verificationProcess.Id -ne $firstPid)
 $name=$window.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,'Restart persistence check'))
 Check-Integration 'Saved plan survives actual exit and restart' ($null -ne $name)
 $all=$window.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::CheckBox))
 $toggles=@($all|Where-Object {$_.Current.AutomationId -eq $waterToggleId})
 Check-Integration 'Enabled state survives actual restart' ($toggles.Count -eq 1 -and ([Windows.Automation.TogglePattern]$toggles[0].GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern)).Current.ToggleState -eq [Windows.Automation.ToggleState]::On)
 Invoke-Integration $window ('Edit_'+$toggles[0].Current.AutomationId.Substring(8))
 $editor=Editor-Integration $window
 $interval=Find-Integration $editor 'IntervalMinutes'
 Check-Integration 'Edited interval survives actual restart' (([Windows.Automation.ValuePattern]$interval.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern)).Current.Value -eq '42')
 Invoke-Integration $editor 'CancelPlan'
 Invoke-Integration $window 'NavSettings';Invoke-Integration $window 'AppearanceSettings'
 $themeChoice=Find-Integration $window 'Theme_violet'
 $selected=$themeChoice.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,'已选择'))
 Check-Integration 'Whole theme selection survives actual process restart' ($null -ne $selected)
 Invoke-Integration $window 'GeneralSettings';$sound=Find-Integration $window 'SoundSetting'
 Check-Integration 'General preference survives actual process restart' (([Windows.Automation.TogglePattern]$sound.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern)).Current.ToggleState -ne $savedSound)
 Invoke-Integration $window 'NavToday';$flowToggle=Find-Integration $window $flowToggleId
 Check-Integration 'One-time completion survives real process restart' (([Windows.Automation.TogglePattern]$flowToggle.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern)).Current.ToggleState -eq [Windows.Automation.ToggleState]::Off)
 Invoke-Integration $window 'NavHistory';Invoke-Integration $window $flowRecordId
 Check-Integration 'Correction history survives real process restart' (Has-IntegrationText $window '原因：Office flow correction')
 Invoke-Integration $window 'CloseRecord'
 Quit-Integration $window
 $success=$true
} finally {
 if(-not $success -and $verificationProcess -and -not $verificationProcess.HasExited -and $verificationProcess.Path -eq $integrationExe){Stop-Process -InputObject $verificationProcess}
 [pscustomobject]@{success=$success;startedAt=$verificationStarted;finishedAt=[DateTimeOffset]::UtcNow.ToString('o');executable=$integrationExe;executableSha256=(Get-FileHash -LiteralPath $integrationExe -Algorithm SHA256).Hash;dataDirectory=$verificationData;checks=$checks;scope='External UIA against published EXE, native save, normal exit, actual process restart, isolated SQLite.'}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $integrationRoot 'artifacts/v039/external-uia-results.json') -Encoding UTF8
}
Write-Output ('External integration verification passed: '+$checks.Count+' checks.')
