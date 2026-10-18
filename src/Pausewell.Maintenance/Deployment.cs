using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

internal sealed record PackageFile(string Path, string Sha256);
internal sealed record ProgramVersion(string Version, string Sha256);
internal sealed record Package(string Version, PackageFile[] Files, ProgramVersion[] CompatiblePrograms);

internal static class Deployment
{
    const string Marker = "pausewell-package.json";
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    static string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
    static bool Within(string child, string parent) => child.StartsWith(parent.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
    static string Child(string root, string relative)
    {
        var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, relative));
        if (!Within(path, root)) throw new IOException("安装包包含无效路径。");
        return path;
    }
    static void CheckTree(string root)
    {
        if (!Directory.Exists(root)) return;
        // Refuse junctions/symlinks before enumeration can traverse them.
        var pending = new Stack<string>(); pending.Push(root);
        while (pending.TryPop(out var current))
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("安装或数据目录含链接，请使用普通目录。");
            foreach (var entry in Directory.EnumerateFileSystemEntries(current))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("目录含链接，已停止操作。");
                if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry);
            }
        }
    }
    static Package ReadPackage(string root)
    {
        var package = JsonSerializer.Deserialize<Package>(File.ReadAllText(System.IO.Path.Combine(root, Marker)), Json)
            ?? throw new InvalidDataException("安装清单无效。");
        if (package.Files.Length == 0 || !package.Files.Any(f => f.Path == "Pausewell.exe")) throw new InvalidDataException("安装清单缺少主程序。");
        foreach (var file in package.Files)
            if (!File.Exists(Child(root, file.Path)) || !Hash(Child(root, file.Path)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("安装包文件校验失败，未修改已安装程序。");
        return package;
    }
    static void CheckOwned(string root, Package package)
    {
        var owned = package.Files.Select(f => Child(root, f.Path)).Append(System.IO.Path.Combine(root, Marker)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Any(f => !owned.Contains(f)))
            throw new IOException("安装目录含清单以外的文件，已停止替换。请保留这些文件并选择新目录。");
    }
    static void RemoveOwned(string root, Package package)
    {
        // Delete only individually enumerated, manifest-owned paths; no recursive delete.
        foreach (var file in package.Files) File.Delete(Child(root, file.Path));
        File.Delete(System.IO.Path.Combine(root, Marker));
        foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(p => p.Length))
            if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
        if (!Directory.EnumerateFileSystemEntries(root).Any()) Directory.Delete(root);
    }
    static void WritePackage(string root, Package package) => File.WriteAllText(System.IO.Path.Combine(root, Marker), JsonSerializer.Serialize(package, Json));
    static void CopyPackage(string source, string destination, Package package)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in package.Files)
        {
            var target = Child(destination, file.Path);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
            File.Copy(Child(source, file.Path), target, false);
        }
        WritePackage(destination, package);
        ReadPackage(destination);
    }
    static void CheckNotRunning(string installRoot)
    {
        foreach (var process in Process.GetProcessesByName("Pausewell"))
        {
            using (process)
            {
                string? path;
                try { path = process.MainModule?.FileName; }
                catch { throw new IOException("无法检查正在运行的间歇，请退出后重试。"); }
                if (path != null && Within(path, installRoot)) throw new IOException("请从系统托盘退出此安装目录中的间歇，再重试。");
            }
        }
    }
    public static object Run(string command, string dataRoot, string installRoot, string source)
    {
        installRoot = System.IO.Path.GetFullPath(installRoot).TrimEnd('\\');
        dataRoot = System.IO.Path.GetFullPath(dataRoot).TrimEnd('\\');
        if (Within(dataRoot, installRoot) || Within(installRoot, dataRoot) || installRoot.Equals(dataRoot, StringComparison.OrdinalIgnoreCase))
            throw new IOException("程序与数据目录必须分开，且不能互相包含。");
        if (installRoot == System.IO.Path.GetPathRoot(installRoot)?.TrimEnd('\\')) throw new IOException("不能使用磁盘根目录。");
        // Check parent links as well as descendants.
        for (var parent = new DirectoryInfo(installRoot); parent != null; parent = parent.Parent)
            if (parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("安装路径含目录链接。");
        CheckTree(installRoot); CheckTree(dataRoot);
        CheckNotRunning(installRoot);
        Package? installed = null;
        if (Directory.Exists(installRoot) && Directory.EnumerateFileSystemEntries(installRoot).Any())
        {
            if (!File.Exists(System.IO.Path.Combine(installRoot, Marker))) throw new IOException("目标目录不是本安装器管理的目录。");
            installed = ReadPackage(installRoot);
            CheckOwned(installRoot, installed);
        }
        var db = System.IO.Path.Combine(dataRoot, "pausewell.db");
        var state = File.Exists(db) ? Snapshot.Inspect(db) : null;
        if (command == "uninstall")
        {
            if (installed == null) throw new IOException("没有可卸载的受管安装。");
            RemoveOwned(installRoot, installed);
            return new { success = true, action = command, dataPreserved = true };
        }
        Package package;
        string? replacement = null;
        if (command == "install")
        {
            source = System.IO.Path.GetFullPath(source);
            if (source.Equals(installRoot, StringComparison.OrdinalIgnoreCase) || Within(source, installRoot) || Within(installRoot, source))
                throw new IOException("请先将安装包解压到独立目录。");
            CheckTree(source); package = ReadPackage(source);
            if (package.Version != "0.3.10") throw new IOException("此维护工具只支持 v0.3.10 安装包。");
            if (installed != null && !package.CompatiblePrograms.Any(p => p.Sha256.Equals(Hash(System.IO.Path.Combine(installRoot, "Pausewell.exe")), StringComparison.OrdinalIgnoreCase)))
                throw new IOException("已安装程序版本不在兼容清单内，未执行升级。");
        }
        else
        {
            if (installed == null) throw new IOException("没有可回退的受管安装。");
            var backupDir = System.IO.Path.GetFullPath(source);
            if (!Within(backupDir, System.IO.Path.Combine(dataRoot, "backups"))) throw new IOException("请选择当前数据目录中的程序备份。");
            CheckTree(backupDir);
            var version = JsonSerializer.Deserialize<ProgramVersion>(File.ReadAllText(System.IO.Path.Combine(backupDir, "program.json")), Json)
                ?? throw new IOException("程序备份清单无效。");
            replacement = System.IO.Path.Combine(backupDir, "Pausewell.exe");
            var hash = Hash(replacement);
            if (!installed.CompatiblePrograms.Any(p => p.Version == version.Version && p.Sha256.Equals(hash, StringComparison.OrdinalIgnoreCase)) || !hash.Equals(version.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new IOException("回退程序校验失败或不在兼容清单内。");
            // All allowlisted versions use schema 3, including the Once schedule and corrections.
            package = installed with { Version = version.Version, Files = installed.Files.Select(f => f.Path == "Pausewell.exe" ? f with { Sha256 = hash } : f).ToArray() };
            source = installRoot;
        }
        var required = package.Files.Sum(f => new FileInfo(Child(source, f.Path)).Length) * 3 + 64L * 1024 * 1024;
        if (new DriveInfo(System.IO.Path.GetPathRoot(installRoot)!).AvailableFreeSpace < required || new DriveInfo(System.IO.Path.GetPathRoot(dataRoot)!).AvailableFreeSpace < required)
            throw new IOException("磁盘空间不足，请保留至少三倍安装包展开空间。");
        var backup = System.IO.Path.Combine(dataRoot, "backups", command + "-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]);
        // Verify all inputs before creating backup directories or changing the installed program.
        Directory.CreateDirectory(backup);
        if (File.Exists(db)) Snapshot.Copy(db, System.IO.Path.Combine(backup, "pausewell.db"));
        if (installed != null)
        {
            File.Copy(System.IO.Path.Combine(installRoot, "Pausewell.exe"), System.IO.Path.Combine(backup, "Pausewell.exe"));
            File.WriteAllText(System.IO.Path.Combine(backup, "program.json"), JsonSerializer.Serialize(new ProgramVersion(installed.Version, Hash(System.IO.Path.Combine(installRoot, "Pausewell.exe"))), Json));
        }
        var staging = installRoot + ".staging-" + Guid.NewGuid().ToString("N");
        var previous = installRoot + ".previous-" + Guid.NewGuid().ToString("N");
        // Staging is on the installation drive; renaming never crosses volumes.
        if (replacement == null) CopyPackage(source, staging, package);
        else
        {
            CopyPackage(source, staging, installed!);
            File.Copy(replacement, System.IO.Path.Combine(staging, "Pausewell.exe"), true);
            WritePackage(staging, package); ReadPackage(staging);
        }
        try
        {
            if (Directory.Exists(installRoot)) Directory.Move(installRoot, previous);
            try { Directory.Move(staging, installRoot); }
            catch { if (Directory.Exists(previous)) Directory.Move(previous, installRoot); throw; }
        }
        finally
        {
            if (Directory.Exists(staging)) RemoveOwned(staging, package);
        }
        if (Directory.Exists(previous))
        {
            if (installed != null) RemoveOwned(previous, installed);
            else if (!Directory.EnumerateFileSystemEntries(previous).Any()) Directory.Delete(previous);
        }
        return new { success = true, action = command, version = package.Version, backup, dataPreserved = true, startupEnabled = state?.startupEnabled ?? false };
    }
}
