# 开发与版本管理

`main` 保存可构建的基线。日常修改从 `main` 创建 `fix/问题简称` 或 `feature/功能简称` 分支，每次提交围绕一项明确修改，完成相关检查后合入。单人开发也不需要额外维护长期 `develop` 分支。

提交信息写清动作和对象，例如 `fix: preserve draft when reopening settings`。需求、功能、界面、数据行为发生变化时，同时更新对应说明及 `CHANGELOG.md`。有真实使用问题时保留可复现步骤，上传截图前先去掉药名和个人记录。

## 首次构建

在 Windows 安装 .NET 10 SDK，或将其放在 `.tools/dotnet`。根目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
```

该脚本先运行核心规则，再发布独立运行的 Windows EXE。SDK、运行数据与生成的 `artifacts` 都不会随 Git 克隆，需要在本机重新生成。历史体验入口指向各自旧包，只有对应包仍在本机时才能使用。

当前安装与办公试用脚本依赖本机的已验证包及测试清单。新克隆能构建当前程序，但不能仅凭源码恢复以前的 EXE 哈希和原生截图。验收摘要放在文档中，原始产物另存；后续发布安装包使用 GitHub Releases。此基线尚未配置云端 CI，不会将不存在的云端检查标为通过。

## 提交前

```powershell
git status --short
git diff --check
git diff --cached --stat
```

确认待提交列表只有源码、必要资源、设计规范和文档，排除数据库、日志、个人试用数据和凭据。二进制主题图片及设计参考图保留，构建生成的截图和程序包留在 `artifacts`。

普通规则修改运行 `tests/Pausewell.Specs`；界面、部署或办公试用工具修改运行对应原生或集成检查。锁屏、睡眠和全天试用只能以实际操作记录验收。

## 标签与恢复

当前基线使用附注标签 `v0.3.10-beta`。后续修复递增版本，标签一经共享不移动、不覆盖。正式版只有完成相应验收后再标记。

查看旧代码时另建目录，保留现有试用程序与数据：

```powershell
git worktree add --detach ../Pausewell-v0.3.10-beta v0.3.10-beta
```

Git 回退只影响源码，不恢复本地数据库。数据恢复使用维护工具并单独确认。不要在正在使用的工作区执行 `git clean -fdx`，该命令会删除 Git 故意忽略的本地 SDK、包和试用数据。

用户已指定使用公开仓库，目标为 `ScauYjj/Pausewell`。仓库创建并确认归属后：

```powershell
git remote add origin https://github.com/ScauYjj/Pausewell.git
git push -u origin main
git push origin v0.3.10-beta
```

推送前核对仓库归属和公开范围，不强推已有远程历史。个人数据、日志和凭据必须持续排除。首次提交身份使用已连接 GitHub 账号的 noreply 地址，仅配置在当前仓库。
