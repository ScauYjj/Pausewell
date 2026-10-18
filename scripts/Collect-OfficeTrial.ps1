param([string]$DataDirectory,[string]$OutputDirectory,[switch]$NoOpen)
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSHOME 'Modules/Microsoft.PowerShell.Utility') -ErrorAction Stop
if(-not $DataDirectory){$DataDirectory=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'PausewellOfficeTrial-v0310'}
$DataDirectory=[IO.Path]::GetFullPath($DataDirectory)
if(-not $OutputDirectory){$OutputDirectory=Join-Path $DataDirectory ('reports/'+[DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,6))}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
$diagnostics=Join-Path $DataDirectory 'diagnostics'
$events=New-Object 'Collections.Generic.List[object]'
$invalid=0;$oversized=0
$allowed=@('start','sample','session_lock','session_unlock','suspend','resume','scheduler_fault','scheduler_retry','reminder_shown','preview_shown','reminder_resolved','reminder_deferred','exit')
foreach($fileName in @('runtime.previous.jsonl','runtime.jsonl')){
 $path=Join-Path $diagnostics $fileName
 if(-not (Test-Path -LiteralPath $path)){continue}
 if((Get-Item -LiteralPath $path).Length -gt 2MB){$oversized++;continue}
 $stream=New-Object IO.FileStream($path,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
 $reader=New-Object IO.StreamReader($stream,[Text.Encoding]::UTF8)
 try{
  while(-not $reader.EndOfStream){
   $line=$reader.ReadLine()
   if([string]::IsNullOrWhiteSpace($line)){continue}
   try{
    $event=$line|ConvertFrom-Json -ErrorAction Stop
    $utc=[DateTimeOffset]::Parse($event.utc)
    if($allowed -notcontains $event.eventName -or $event.run -notmatch '^[a-fA-F0-9]{32}$'){throw 'Invalid event identity'}
    # Keep a strict field allowlist. Never copy raw events, IDs, arbitrary strings or exception text.
    $sample=$null
    if($event.eventName -eq 'sample'){
     foreach($field in @('privateBytes','workingSetBytes','cpuSeconds')){
      $value=$event.fields.$field
      if($null -eq $value -or $value -is [string] -or $value -is [bool] -or [double]::IsNaN([double]$value) -or [double]::IsInfinity([double]$value) -or [double]$value -lt 0){throw 'Invalid numeric sample'}
     }
     $sample=[pscustomobject]@{privateBytes=[double]$event.fields.privateBytes;workingSetBytes=[double]$event.fields.workingSetBytes;cpuSeconds=[double]$event.fields.cpuSeconds}
    }
    $origin=if($event.fields.origin -eq 'os'){'os'}elseif($event.fields.origin -eq 'verification'){'verification'}else{'unknown'}
    $delay=$null
    if($event.eventName -eq 'reminder_shown' -and $null -ne $event.fields.delaySeconds -and $event.fields.delaySeconds -isnot [string]){
     $candidate=[double]$event.fields.delaySeconds
     if(-not [double]::IsNaN($candidate) -and -not [double]::IsInfinity($candidate) -and $candidate -ge 0){$delay=$candidate}
    }
    $events.Add([pscustomobject]@{utc=$utc;run=$event.run;eventName=$event.eventName;origin=$origin;sample=$sample;delay=$delay})
   }catch{$invalid++}
  }
 }finally{$reader.Dispose()}
}
$runs=New-Object 'Collections.Generic.List[object]'
$runNumber=0
foreach($group in @($events|Group-Object run)){
 $runNumber++;$ordered=@($group.Group|Sort-Object utc);$samples=@($ordered|Where-Object {$_.eventName -eq 'sample'})
 $gaps=@();for($i=1;$i -lt $samples.Count;$i++){$gaps+=($samples[$i].utc-$samples[$i-1].utc).TotalSeconds}
 $cpu=$null
 if($samples.Count -gt 1 -and $samples[-1].sample.cpuSeconds -ge $samples[0].sample.cpuSeconds){$cpu=[math]::Round(($samples[-1].sample.cpuSeconds-$samples[0].sample.cpuSeconds),3)}
 $firstMemory=if($samples.Count){[math]::Round($samples[0].sample.privateBytes/1MB,2)}else{$null}
 $lastMemory=if($samples.Count){[math]::Round($samples[-1].sample.privateBytes/1MB,2)}else{$null}
 $peakMemory=if($samples.Count){[math]::Round(($samples|ForEach-Object {$_.sample.privateBytes}|Measure-Object -Maximum).Maximum/1MB,2)}else{$null}
 $runs.Add([pscustomobject]@{run=$runNumber;firstUtc=$ordered[0].utc.ToUniversalTime().ToString('o');lastUtc=$ordered[-1].utc.ToUniversalTime().ToString('o');samples=$samples.Count;firstPrivateMiB=$firstMemory;lastPrivateMiB=$lastMemory;peakSampledPrivateMiB=$peakMemory;sampledCpuDeltaSeconds=$cpu;maxSampleGapSeconds=if($gaps.Count){[math]::Round(($gaps|Measure-Object -Maximum).Maximum,1)}else{$null};normalExitSeen=(@($ordered|Where-Object {$_.eventName -eq 'exit'}).Count -gt 0)})
}
$counts=[ordered]@{}
foreach($name in $allowed){$counts[$name]=@($events|Where-Object {$_.eventName -eq $name}).Count}
$osSignals=[ordered]@{};$syntheticSignals=[ordered]@{}
foreach($name in @('session_lock','session_unlock','suspend','resume')){
 $osSignals[$name]=@($events|Where-Object {$_.eventName -eq $name -and $_.origin -eq 'os'}).Count
 $syntheticSignals[$name]=@($events|Where-Object {$_.eventName -eq $name -and $_.origin -eq 'verification'}).Count
}
$delays=@($events|Where-Object {$_.eventName -eq 'reminder_shown' -and $null -ne $_.delay}|ForEach-Object {$_.delay})
$report=[ordered]@{formatVersion=1;createdAtUtc=[DateTimeOffset]::UtcNow.ToString('o');appVersion='0.3.10';eventCount=$events.Count;invalidLines=$invalid;oversizedFilesSkipped=$oversized;eventCounts=$counts;osSignals=$osSignals;syntheticSignals=$syntheticSignals;runs=$runs.ToArray();maxObservedDueToDisplaySeconds=if($delays.Count){[math]::Round(($delays|Measure-Object -Maximum).Maximum,3)}else{$null};allDayAccepted=$false;realDeviceAccepted=$false;interpretation='Due-to-display duration includes meetings, sleep and deferral; it is not scheduler latency. Missing normal exit can mean the app is still running. Log rotation and sampling gaps limit coverage. Manual acceptance remains required.'}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$utf8=New-Object Text.UTF8Encoding($true)
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'office-trial.json'),($report|ConvertTo-Json -Depth 8),$utf8)
$lines=New-Object 'Collections.Generic.List[string]'
$lines.Add('# 间歇办公试用记录')
$lines.Add('')
$lines.Add('这是本机日志的有限观测汇总，不自动判定全天或设备验收通过。不包含事项名称、药名、备注、记录正文或原始日志。')
$lines.Add('')
$lines.Add(('有效事件：{0}；运行段：{1}；无法解析或不受支持的行：{2}；跳过超限文件：{3}。' -f $events.Count,$runs.Count,$invalid,$oversized))
$lines.Add(('提醒展示 {0} 次，确认处理 {1} 次，调度故障 {2} 次。展示次数包含同一待办再次展示，不能据此判断重复提醒。' -f $counts.reminder_shown,$counts.reminder_resolved,$counts.scheduler_fault))
$lines.Add('')
$lines.Add('| 运行段 | 采样数 | 私有内存初/末/最高 MiB | 采样间最大间隔 秒 | 正常退出日志 |')
$lines.Add('| --- | --- | --- | --- | --- |')
foreach($run in $runs){$lines.Add(('| {0} | {1} | {2} / {3} / {4} | {5} | {6} |' -f $run.run,$run.samples,$run.firstPrivateMiB,$run.lastPrivateMiB,$run.peakSampledPrivateMiB,$run.maxSampleGapSeconds,$run.normalExitSeen))}
$lines.Add('')
$lines.Add(('真实系统信号：锁屏 {0}，解锁 {1}，睡眠 {2}，恢复 {3}。仅看到信号不代表该场景已验收。' -f $osSignals.session_lock,$osSignals.session_unlock,$osSignals.suspend,$osSignals.resume))
$lines.Add(('工程合成信号：锁屏 {0}，解锁 {1}，睡眠 {2}，恢复 {3}。这部分不能当作真实设备结果。' -f $syntheticSignals.session_lock,$syntheticSignals.session_unlock,$syntheticSignals.suspend,$syntheticSignals.resume))
$lines.Add('')
$lines.Add('注意：计划时刻到展示的间隔可能包含会议、睡眠和延期，不等于程序调度延迟。资源按单次进程分别统计，短时曲线不能证明没有内存泄漏。未见退出日志可能表示程序仍在运行，采样空档和日志轮换会限制可观察范围。')
$lines.Add('')
$lines.Add('## 待人工填写')
$lines.Add('')
$lines.Add('- 试用起止时间及实际在岗时长：')
$lines.Add('- 漏提醒、重复提醒或异常窗口：')
$lines.Add('- 会议、锁屏、睡眠、多屏的实际操作与结果：')
$lines.Add('- 问题复现步骤及截图：')
[IO.File]::WriteAllText((Join-Path $OutputDirectory '办公试用记录.md'),($lines -join "`r`n"),$utf8)
if($NoOpen){[pscustomobject]@{outputDirectory=$OutputDirectory;report=(Join-Path $OutputDirectory 'office-trial.json')}|ConvertTo-Json}
else{Write-Host ('试用记录已生成：'+$OutputDirectory);Start-Process -FilePath explorer.exe -ArgumentList ('"'+$OutputDirectory+'"')}
