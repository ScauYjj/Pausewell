param([switch]$RefreshPackage)
$ErrorActionPreference='Stop'
$workspace=Split-Path -Parent $PSScriptRoot
$dotnet=Join-Path $workspace '.tools/dotnet/dotnet.exe'
if(-not (Test-Path -LiteralPath $dotnet)){$dotnet=(Get-Command dotnet -ErrorAction Stop).Source}
& (Join-Path $PSScriptRoot 'build.ps1')
if($LASTEXITCODE -ne 0){throw 'Current application build failed.'}
$program=Join-Path $workspace 'artifacts/publish/win-x64/Pausewell.exe'
$programHash=(Get-FileHash -LiteralPath $program).Hash
$output=Join-Path $workspace 'artifacts/packages'
$bundle=Join-Path $output 'Pausewell-0.3.10-win-x64'
if((Test-Path -LiteralPath $bundle) -and -not $RefreshPackage){throw 'Output already exists. Use -RefreshPackage only for this regenerable package output.'}
New-Item -ItemType Directory -Path $bundle -Force | Out-Null
& $dotnet publish (Join-Path $workspace 'src/Pausewell.Maintenance/Pausewell.Maintenance.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=None -o (Join-Path $output 'maintenance-build') --nologo
if($LASTEXITCODE -ne 0){throw 'Maintenance build failed.'}
New-Item -ItemType Directory -Path (Join-Path $bundle 'maintenance'),(Join-Path $bundle 'licenses') -Force | Out-Null
Copy-Item -LiteralPath $program -Destination (Join-Path $bundle 'Pausewell.exe') -Force
Copy-Item -LiteralPath (Join-Path $output 'maintenance-build/Pausewell.Maintenance.exe') -Destination (Join-Path $bundle 'maintenance/Pausewell.Maintenance.exe') -Force
Copy-Item -LiteralPath (Join-Path $workspace 'src/Pausewell.Desktop/Assets/Icons/LICENSE.txt') -Destination (Join-Path $bundle 'licenses/Fluent-System-Icons.txt') -Force
$utf8Bom=New-Object Text.UTF8Encoding($true)
[IO.File]::WriteAllText((Join-Path $bundle 'Manage-Pausewell.ps1'),[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Manage-Pausewell.ps1')),$utf8Bom)
$cmd='@echo off'+"`r`n"+'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Manage-Pausewell.ps1" -Action install -Interactive'+"`r`n"+'pause'+"`r`n"
[IO.File]::WriteAllText((Join-Path $bundle 'Install.cmd'),$cmd,[Text.Encoding]::ASCII)
$guide=@'
间歇 Pausewell v0.3.10 安装试用包

完整解压到独立文件夹，双击 Install.cmd。无需管理员权限，无需另装 .NET。
首次使用不会自动创建计划、自动运行或启用开机启动；已有数据中的开机启动设置会沿用。请在开始菜单打开“间歇 Pausewell”。
程序默认位置：%LOCALAPPDATA%\Programs\Pausewell
数据沿用：%LOCALAPPDATA%\PausewellPrototype
升级前先从托盘退出旧版。安装会检查文件哈希、数据版本与空间，并把旧程序和数据库备份到数据目录 backups。
如果提示开机启动指向另一安装位置，请先在原程序中关闭开机启动，再退出并安装。
现有项目的 demo 数据不会自动迁移。普通安装首次打开可能为空，这是与演示数据分开的真实使用目录。

卸载：Windows 设置 > 应用 > 间歇 Pausewell（试用版），或在 PowerShell 运行：
  .\Manage-Pausewell.ps1 -Action uninstall
卸载保留数据库、备份和最新执行记录。开机启动项只在指向此安装时清除。

程序回退：先退出应用，找到 backups 中带 program.json 的文件夹，再运行：
  .\Manage-Pausewell.ps1 -Action rollback -SourcePath "完整备份文件夹路径"
只回退主程序，保留最新执行记录。目前只允许校验过的 v0.3.9 与 v0.3.10。

手动备份：
  .\Manage-Pausewell.ps1 -Action backup
数据恢复：会替换当前记录，备份之后新增的记录将不在恢复后的视图中。先退出应用，再运行：
  .\Manage-Pausewell.ps1 -Action restore -SourcePath "数据库备份完整路径" -ConfirmDataRestore
恢复前另存当前数据库，保留在 backups/before-restore-*，不会自动把记录视为完成。
备份含个人提醒记录，请在本机妥善保管。本包未签名，尚未完成真实锁屏、睡眠、多屏与全天试用验收。
当前提供可审查的一键安装脚本，标准安装向导和自动更新尚未交付。
'@
[IO.File]::WriteAllText((Join-Path $bundle '使用说明.txt'),$guide,$utf8Bom)
$compatible=@(
 @{version='0.3.9';sha256='D12DD90B0F6C6FEDD7560577B6DE9929A293B973EE4501DB9D81F83755076028'},
 @{version='0.3.10';sha256='778162BFBE886654DFBAA18BC85651C1ECEF3543544003964976A7111DEEE97B'},
 @{version='0.3.10';sha256=$programHash}
)
$files=@(Get-ChildItem -LiteralPath $bundle -File -Recurse | Where-Object {$_.Name -ne 'pausewell-package.json'} | ForEach-Object {@{path=$_.FullName.Substring($bundle.Length+1).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
@{version='0.3.10';files=$files;compatiblePrograms=$compatible} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $bundle 'pausewell-package.json') -Encoding UTF8
$archive=Join-Path $output 'Pausewell-0.3.10-win-x64-install.zip'
Compress-Archive -LiteralPath $bundle -DestinationPath $archive -CompressionLevel Optimal -Force
@{createdAt=[DateTimeOffset]::UtcNow.ToString('o');appVersion='0.3.10';appSha256=$programHash;archive=$archive;sha256=(Get-FileHash -LiteralPath $archive).Hash;compiler='local .NET SDK';signed=$false;standardInstaller=$false} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'package-manifest.json') -Encoding UTF8
Write-Output $archive
