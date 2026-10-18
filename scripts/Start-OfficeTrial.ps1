param([string]$DataDirectory,[switch]$NoLaunch)
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSHOME 'Modules/Microsoft.PowerShell.Utility') -ErrorAction Stop
$workspace=Split-Path -Parent $PSScriptRoot
$manifest=Get-Content -LiteralPath (Join-Path $workspace 'artifacts/v0310/release-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$installed=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs/Pausewell/Pausewell.exe'
$executable=$manifest.newPackage.path
$source='published'
if((Test-Path -LiteralPath $installed) -and (Get-FileHash -LiteralPath $installed).Hash -eq $manifest.newPackage.sha256){$executable=$installed;$source='installed'}
if(-not (Test-Path -LiteralPath $executable) -or (Get-FileHash -LiteralPath $executable).Hash -ne $manifest.newPackage.sha256){throw '试用程序与已验证版本不一致，已停止启动。'}
if(-not $DataDirectory){$DataDirectory=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'PausewellOfficeTrial-v0310'}
$DataDirectory=[IO.Path]::GetFullPath($DataDirectory).TrimEnd('\')
$normalData=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'PausewellPrototype'
if($DataDirectory -eq $normalData -or $DataDirectory.StartsWith($normalData+'\',[StringComparison]::OrdinalIgnoreCase)){throw '办公试用不能使用正常数据目录。'}
$sha=[Security.Cryptography.SHA256]::Create()
try{$key=([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($DataDirectory.ToUpperInvariant())))).Replace('-','').Substring(0,16)}finally{$sha.Dispose()}
$existingMutex=$null
$running=[Threading.Mutex]::TryOpenExisting(('Local\Pausewell.Prototype.'+$key),[ref]$existingMutex)
if($existingMutex){$existingMutex.Dispose()}
if($NoLaunch){[pscustomobject]@{version='0.3.10';source=$source;executable=$executable;sha256=$manifest.newPackage.sha256;dataDirectory=$DataDirectory;alreadyRunning=$running}|ConvertTo-Json;return}
if($running){Write-Host '办公试用已经在运行，请从系统托盘打开“间歇”。';return}
New-Item -ItemType Directory -Path $DataDirectory -Force | Out-Null
$trialProcess=Start-Process -FilePath $executable -ArgumentList ('--diagnostics --data-dir "'+$DataDirectory+'"') -WorkingDirectory (Split-Path -Parent $executable) -WindowStyle Normal -PassThru
Start-Sleep -Milliseconds 700
$trialProcess.Refresh()
if($trialProcess.HasExited){throw '办公试用未能保持运行，请检查窗口提示。'}
Write-Host '办公试用已打开。首次使用为空白，请创建自己需要的提醒。'
Write-Host '关闭窗口后继续在托盘提醒；设置中的“退出应用”会停止提醒。'
