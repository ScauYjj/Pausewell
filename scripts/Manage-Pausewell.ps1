param(
 [ValidateSet('install','rollback','uninstall','backup','restore')][string]$Action='install',
 [string]$InstallRoot,
 [string]$DataRoot,
 [string]$SourcePath,
 [switch]$Isolated,
 [switch]$ConfirmDataRestore,
 [switch]$Interactive
)
$ErrorActionPreference='Stop'
# Native callers may inherit another PowerShell edition's PSModulePath.
Import-Module (Join-Path $PSHOME 'Modules/Microsoft.PowerShell.Utility') -ErrorAction Stop
if($Isolated -and (-not $InstallRoot -or -not $DataRoot)){throw '隔离验证必须指定程序和数据目录。'}
$normalData=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'PausewellPrototype'
if(-not $InstallRoot){$InstallRoot=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs/Pausewell'}
if(-not $DataRoot){$DataRoot=$normalData}
$InstallRoot=[IO.Path]::GetFullPath($InstallRoot).TrimEnd('\')
$DataRoot=[IO.Path]::GetFullPath($DataRoot).TrimEnd('\')
if(-not $Isolated -and $DataRoot -ne $normalData){throw '自定义数据目录仅用于显式隔离验证。普通安装沿用现有数据目录。'}
$bundleRoot=$PSScriptRoot
$helper=Join-Path $bundleRoot 'maintenance/Pausewell.Maintenance.exe'
$packageManifest=Join-Path $bundleRoot 'pausewell-package.json'
if(-not (Test-Path -LiteralPath $packageManifest)){throw '请从完整解压的安装包中运行此脚本。'}
$package=Get-Content -LiteralPath $packageManifest -Raw -Encoding UTF8 | ConvertFrom-Json
$helperHash=($package.files | Where-Object {$_.path -eq 'maintenance/Pausewell.Maintenance.exe'}).sha256
if(-not $helperHash -or (Get-FileHash -LiteralPath $helper -Algorithm SHA256).Hash -ne $helperHash){throw '维护工具校验失败，已停止。'}
$uninstallKey='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Pausewell'
$shortcut=Join-Path ([Environment]::GetFolderPath('Programs')) '间歇 Pausewell.lnk'
if(-not $Isolated){
 $runKey='HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
 $ownedStartup='"'+(Join-Path $InstallRoot 'Pausewell.exe')+'" --background'
 $existingStartup=(Get-ItemProperty -LiteralPath $runKey -Name Pausewell -ErrorAction SilentlyContinue).Pausewell
 if($Action -eq 'install' -and $existingStartup -and $existingStartup -ne $ownedStartup){throw '开机启动目前指向另一安装位置。请先在原程序中关闭开机启动，再安装本版本。'}
 if(Test-Path -LiteralPath $uninstallKey){
  $registered=(Get-ItemProperty -LiteralPath $uninstallKey).InstallLocation
  if($registered -ne $InstallRoot){throw '已有其他安装位置登记，请先使用原安装位置或卸载原安装。'}
 }
 if(Test-Path -LiteralPath $shortcut){
  $existingShortcut=(New-Object -ComObject WScript.Shell).CreateShortcut($shortcut)
  if($existingShortcut.TargetPath -ne (Join-Path $InstallRoot 'Pausewell.exe')){throw '同名快捷方式属于其他程序，请先调整其名称。'}
 }
}
# Run a verified copy outside the installation tree, so uninstall never deletes its running helper.
$temporary=Join-Path ([IO.Path]::GetTempPath()) ('Pausewell-maintenance-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporary | Out-Null
$temporaryHelper=Join-Path $temporary 'Pausewell.Maintenance.exe'
Copy-Item -LiteralPath $helper -Destination $temporaryHelper
try {
 $arguments=@($Action,$DataRoot)
 switch($Action){
  'install' {$arguments+=@($InstallRoot,$bundleRoot)}
  'rollback' {if(-not $SourcePath){throw '请选择数据目录 backups 中的程序备份文件夹。'};$arguments+=@($InstallRoot,$SourcePath)}
  'uninstall' {$arguments+=@($InstallRoot,'-')}
  'backup' {if(-not $SourcePath){$SourcePath=Join-Path $DataRoot ('backups/manual-'+[guid]::NewGuid().ToString('N')+'.db')};$arguments+=$SourcePath}
  'restore' {if(-not $ConfirmDataRestore){throw '恢复会替换当前记录。确认后使用 -ConfirmDataRestore，恢复前仍会自动备份。'};if(-not $SourcePath){throw '请选择需要恢复的数据库备份。'};$arguments+=@($SourcePath,'--confirm-replace-data')}
 }
 $maintenanceOutput=& $temporaryHelper @arguments
 if($LASTEXITCODE -ne 0){throw '操作失败，详见上方提示。没有自动清空数据。'}
 $result=$maintenanceOutput | ConvertFrom-Json
 if(-not $Isolated -and $Action -eq 'install'){
  New-Item -Path $uninstallKey -Force | Out-Null
  $uninstallCommand='powershell.exe -NoProfile -ExecutionPolicy Bypass -File "'+(Join-Path $InstallRoot 'Manage-Pausewell.ps1')+'" -Action uninstall -InstallRoot "'+$InstallRoot+'"'
  $registryValues=@{DisplayName='间歇 Pausewell（试用版）';DisplayVersion=$result.version;Publisher='Pausewell';InstallLocation=$InstallRoot;DisplayIcon=(Join-Path $InstallRoot 'Pausewell.exe');UninstallString=$uninstallCommand}
  foreach($entry in $registryValues.GetEnumerator()){New-ItemProperty -LiteralPath $uninstallKey -Name $entry.Key -Value $entry.Value -PropertyType String -Force | Out-Null}
  New-ItemProperty -LiteralPath $uninstallKey -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
  New-ItemProperty -LiteralPath $uninstallKey -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null
  $link=(New-Object -ComObject WScript.Shell).CreateShortcut($shortcut)
  $link.TargetPath=Join-Path $InstallRoot 'Pausewell.exe';$link.WorkingDirectory=$InstallRoot;$link.Save()
  # A preserved user preference is restored on reinstall; a new install never opts in.
  if($result.startupEnabled){
   New-Item -Path $runKey -Force | Out-Null
   New-ItemProperty -LiteralPath $runKey -Name Pausewell -Value $ownedStartup -PropertyType String -Force | Out-Null
  }
 }
 if(-not $Isolated -and $Action -eq 'rollback' -and (Test-Path -LiteralPath $uninstallKey)){
  Set-ItemProperty -LiteralPath $uninstallKey -Name DisplayVersion -Value $result.version
 }
 if(-not $Isolated -and $Action -eq 'uninstall'){
  $runKey='HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
  $startup=(Get-ItemProperty -LiteralPath $runKey -Name Pausewell -ErrorAction SilentlyContinue).Pausewell
  if($startup -eq ('"'+(Join-Path $InstallRoot 'Pausewell.exe')+'" --background')){Remove-ItemProperty -LiteralPath $runKey -Name Pausewell}
  if(Test-Path -LiteralPath $shortcut){Remove-Item -LiteralPath $shortcut}
  if(Test-Path -LiteralPath $uninstallKey){Remove-Item -LiteralPath $uninstallKey}
 }
 if($Interactive){
  Write-Host '安装完成。请从开始菜单打开“间歇 Pausewell”。'
  Write-Host '已有提醒记录已保留；首次使用请自行创建提醒。'
 } else {$result | ConvertTo-Json -Depth 10}
} finally {
 # Only remove the one verified helper created in this exact temporary directory.
 if((Split-Path -Parent $temporaryHelper) -ne $temporary -or (Split-Path -Leaf $temporary) -notlike 'Pausewell-maintenance-*'){throw '临时路径校验失败。'}
 if(Test-Path -LiteralPath $temporaryHelper){Remove-Item -LiteralPath $temporaryHelper}
 if(-not (Get-ChildItem -LiteralPath $temporary -Force)){Remove-Item -LiteralPath $temporary}
}
