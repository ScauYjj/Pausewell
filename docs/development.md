# 开发说明

间歇是 Windows 本地桌面程序，使用 C#、WPF、.NET 10 与 SQLite，核心计划和记录保存在本机。产品用途与界面演示见仓库首页。

## 代码结构

| 目录 | 职责 |
| --- | --- |
| `src/Pausewell.Desktop` | 主窗口、提醒与设置页面、托盘及 Windows 会话恢复 |
| `src/Pausewell.Core` | 提醒规则、会议与暂停、一次性事项、服药记录及更正 |
| `src/Pausewell.Storage` | SQLite 本地保存与一致性备份 |
| `src/Pausewell.Controls` | 共享输入、日期、状态控件与设计资源 |
| `src/Pausewell.Presentation` | 表单、主题材质与通用页面布局 |
| `src/Pausewell.Maintenance` | 安装、备份、数据恢复与受限程序回退 |
| `tests` | 提醒规则与安装恢复的有效测试 |
| `scripts` | 构建、验证和打包所需的少量脚本 |
| `docs/screenshots` | 经过检查的产品演示截图 |

程序的真实材质和控件资源已归入 `src`，构建不再依赖旧的打样目录。部分类型仍保留早期命名空间，以减少整理目录时的行为变化。

## 构建与运行

在 Windows 准备 .NET 10 SDK 和 PowerShell。SDK 可安装到系统，也可放在 `.tools/dotnet`。根目录运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
```

脚本先验证核心规则，再生成可独立运行的 `artifacts/publish/win-x64/Pausewell.exe`。首次构建需要还原 SDK 对应的依赖。

开发时建议使用独立数据，避免改动已有计划：

```powershell
.\artifacts\publish\win-x64\Pausewell.exe --data-dir "D:\Pausewell-dev-data"
```

此目录初次为空白。增加 `--demo` 可以初始化演示事项，其中普通示例会真实触发提醒；停止提醒使用“设置 > 退出应用”。独立数据模式不修改 Windows 登录启动项。

正常启动不传 `--data-dir`，使用 `%LOCALAPPDATA%/PausewellPrototype/pausewell.db`。关闭窗口默认驻留托盘，退出应用会停止提醒。需要运行观测时可以显式增加 `--diagnostics`，数据仍留在本机。

## 验证

只运行核心规则：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1 -RulesOnly
```

原生界面回归：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
```

原生检查使用临时独立数据，运行期间会显示测试窗口，并包含真实时钟到期与短暂全屏。报告写入 `artifacts/verification/gui`。真实锁屏、睡眠、多屏、共享与全天使用需要实际设备验收，不能由合成信号代替。

## 打包与安装恢复验证

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-install-package.ps1
dotnet run --project tests/Pausewell.Deployment.Specs -c Release -- .
```

打包脚本构建当前源码，生成 `artifacts/packages/Pausewell-0.4.0-win-x64-install.zip`，不依赖作者机器上的历史包或测试清单。重复生成可使用 `-RefreshPackage`。

完整解压后运行 `Install.cmd`。这个 CMD 是安装包的启动入口，由打包时自动生成，不纳入源码目录。安装和维护通过包内 `Manage-Pausewell.ps1` 执行。v0.4 使用 schema 4，打开旧数据库前创建一致性备份，历史结果保留。数据升级后不允许仅把程序退回 v0.3，避免旧程序忽略新规则。

安装恢复测试使用当前包建立受管安装夹具，检查备份、回退保留最新记录、数据恢复确认、坏包拒绝及卸载保留数据，不读真实用户目录。本轮可重复测试不依赖旧 v0.3.9 程序；既有跨版本验收记录保留在历史 Beta 标签，不把同版本夹具测试说成重新通过跨版本验收。

## 仓库范围

主分支保留当前产品源码、资源、有效测试、必要脚本和产品说明。历史原型、逐轮截图比较、旧启动 CMD、个人试用日志与数据库保留在作者本机及历史版本，不进入当前主分支文件列表。已有历史不会被强制改写。

截图均来自独立虚构数据的实际运行窗口，提醒弹窗为自带预览。桌面程序当前版本为 v0.4.0，新增恢复规则与删除操作；较早的 v0.3.10 演示图以首页标注为准。Microsoft Fluent 图标许可保留在相应资源目录。

## 列表行操作

习惯、待办与服药计划的编辑和删除统一使用 `Ui.RowAction`，样式集中在 `WorkbenchStyles.xaml` 的 `RowActionButton`、`RowDeleteButton`。默认透明底、无边框、次要文字色，使用 14px 线形图标、13px 文字、36px 点击高度、10px 圆角和 4px 操作间距。编辑悬停及键盘聚焦使用当前主题浅底色；删除仅在交互时使用语义红色与浅红底。图标和文字绑定按钮前景色，禁止在各页面单独写死颜色或恢复为表单按钮样式。

## 系统时间

显示、日期输入、固定时刻和办公时段统一使用 Windows 本地时区对象，包含系统关闭夏令时的设置。启动时迁移旧的固定中国时区配置，运行中响应时间变化并定期检查规则。历史记录、一次性提醒和截止时间保留原来的绝对时间点；切换时区不补列新时区中已经过去的固定时刻。间隔提醒保留未来到期点，仅在不符合新的办公时段时顺延。历史固定提醒通过实际到期点去重，避免时区切换后的身份冲突。
