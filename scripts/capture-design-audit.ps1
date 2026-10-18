param()
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,System.Drawing
$auditRoot=Split-Path -Parent $PSScriptRoot
$auditFolder=Join-Path $auditRoot 'artifacts/design-v03/audit'
[IO.Directory]::CreateDirectory($auditFolder)|Out-Null
Add-Type @'
using System;using System.Runtime.InteropServices;
public static class AuditCapture {
 [StructLayout(LayoutKind.Sequential)] public struct Rect {public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h,IntPtr dc,uint flags);
 [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
 [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
}
'@
function Find-AuditControl($root,[string]$id) {
 $condition=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$id)
 for($i=0;$i -lt 40;$i++){ $control=$root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition);if($control){return $control};Start-Sleep -Milliseconds 100 }
 throw ('Missing control '+$id)
}
function Invoke-AuditControl($root,[string]$id) {
 (Find-AuditControl $root $id).GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 300
}
function Find-AuditWindow([string]$title) {
 $condition=[System.Windows.Automation.AndCondition]::new([System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$auditProcess.Id),[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$title))
 for($i=0;$i -lt 40;$i++){ $element=[System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition);if($element){return $element};Start-Sleep -Milliseconds 100 }
 throw ('Missing window '+$title)
}
$evidence=[System.Collections.Generic.List[object]]::new()
function Save-AuditCapture($element,[string]$name) {
 $handle=[IntPtr]$element.Current.NativeWindowHandle
 $rect=[AuditCapture+Rect]::new();[AuditCapture]::GetClientRect($handle,[ref]$rect)|Out-Null
 $bitmap=[Drawing.Bitmap]::new($rect.Right,$rect.Bottom)
 $graphics=[Drawing.Graphics]::FromImage($bitmap);$dc=$graphics.GetHdc()
 try {if(-not [AuditCapture]::PrintWindow($handle,$dc,3)){throw 'PrintWindow failed'}}finally{$graphics.ReleaseHdc($dc);$graphics.Dispose()}
 try{$bitmap.Save((Join-Path $auditFolder ($name+'.png')),[Drawing.Imaging.ImageFormat]::Png)}finally{$bitmap.Dispose()}
 $evidence.Add([pscustomobject]@{step=$name;window=$element.Current.Name;width=$rect.Right;height=$rect.Bottom;dpi=[AuditCapture]::GetDpiForWindow($handle);capturedAt=[DateTimeOffset]::Now.ToString('o')})
}
$auditData=Join-Path $auditRoot ('artifacts/design-v03/audit-data/'+[Guid]::NewGuid().ToString('N'))
$auditProcess=Start-Process -FilePath (Join-Path $auditRoot 'artifacts/Pausewell-ui-v02/Pausewell.exe') -ArgumentList @('--demo','--data-dir',('"'+$auditData+'"')) -WorkingDirectory $auditRoot -PassThru
try {
 $main=Find-AuditWindow '间歇 · Windows v0.2';$handle=[IntPtr]$main.Current.NativeWindowHandle;$scale=[AuditCapture]::GetDpiForWindow($handle)/96.0
 [AuditCapture]::SetWindowPos($handle,[IntPtr]::Zero,0,0,[int](1004*$scale),[int](866*$scale),6)|Out-Null;Start-Sleep -Milliseconds 350
 Save-AuditCapture $main '01-home'
 Invoke-AuditControl $main 'NewPlan';$form=Find-AuditWindow '新增提醒';Save-AuditCapture $form '02-plan-editor';[AuditCapture]::SendMessage([IntPtr]$form.Current.NativeWindowHandle,16,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null
 Invoke-AuditControl $main 'NavMedicine';Save-AuditCapture $main '03-medicine'
 Invoke-AuditControl $main 'AddMedicine';$form=Find-AuditWindow '新增药物计划';Save-AuditCapture $form '04-medicine-editor';[AuditCapture]::SendMessage([IntPtr]$form.Current.NativeWindowHandle,16,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null
 Invoke-AuditControl $main 'NavHistory';Save-AuditCapture $main '05-history'
 Invoke-AuditControl $main 'NavSettings';Save-AuditCapture $main '06-settings'
 Invoke-AuditControl $main 'AppearanceSettings';Save-AuditCapture $main '07-appearance'
 Invoke-AuditControl $main 'GeneralSettings';Invoke-AuditControl $main 'PreviewWater';$water=Find-AuditWindow '间歇 · 外观预览';Save-AuditCapture $water '08-reminder';Invoke-AuditControl $water 'DeferReminder'
 Invoke-AuditControl $main 'NavToday';Invoke-AuditControl $main 'Meeting30';$mode=Find-AuditWindow '开始会议';Save-AuditCapture $mode '09-meeting';Invoke-AuditControl $mode 'CancelMode'
 Invoke-AuditControl $main 'NavSettings';Invoke-AuditControl $main 'GeneralSettings';Invoke-AuditControl $main 'QuitApp'
 [pscustomobject]@{capturedAt=[DateTimeOffset]::Now.ToString('o');dataDirectory=$auditData;processId=$auditProcess.Id;captures=$evidence;note='Fresh isolated data. No plans or settings saved. Existing app processes untouched.'}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $auditFolder 'capture-manifest.json') -Encoding UTF8
 Write-Output 'Fresh native audit captures saved.'
} catch {
 $evidence|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $auditFolder 'partial-captures.json') -Encoding UTF8
 throw
}
