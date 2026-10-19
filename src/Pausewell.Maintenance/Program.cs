using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pausewell.Core;

// No reminder engine: maintenance must never evaluate or rewrite medication records.
try
{
    if (args.Length < 2) throw new ArgumentException("Command and data directory are required.");
    var command = args[0];
    var dataDir = Path.GetFullPath(args[1]).TrimEnd(Path.DirectorySeparatorChar);
    var mutexName = "Local\\Pausewell.Prototype." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dataDir.ToUpperInvariant())))[..16];
    using var mutex = new Mutex(true, mutexName, out var first);
    if (!first) throw new IOException("请先从系统托盘退出使用此数据目录的间歇，再重试。");
    var db = Path.Combine(dataDir, "pausewell.db");
    if (command is "install" or "rollback" or "uninstall")
    {
        if (args.Length != 4) throw new ArgumentException("Install command requires install root and bundle/source.");
        Console.WriteLine(JsonSerializer.Serialize(Deployment.Run(command, dataDir, args[2], args[3])));
    }
    else if (command == "probe")
    {
        Console.WriteLine(File.Exists(db) ? JsonSerializer.Serialize(Snapshot.Inspect(db)) : "{\"exists\":false}");
    }
    else if (command == "backup")
    {
        if (args.Length != 3) throw new ArgumentException("Backup requires a new destination file.");
        Snapshot.Copy(db, args[2]);
        Console.WriteLine(JsonSerializer.Serialize(Snapshot.Inspect(args[2])));
    }
    else if (command == "restore")
    {
        if (args.Length != 4 || args[3] != "--confirm-replace-data")
            throw new ArgumentException("恢复会替换当前记录。必须提供 --confirm-replace-data。");
        var source = Path.GetFullPath(args[2]);
        if (source.Equals(db, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("请选择独立备份文件。");
        Snapshot.Inspect(source);
        Directory.CreateDirectory(dataDir);
        var recovery = Path.Combine(dataDir, "backups", "before-restore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(recovery);
        var staged = Path.Combine(dataDir, "restore-" + Guid.NewGuid().ToString("N") + ".db");
        Snapshot.Copy(source, staged);
        var moved = new List<(string From, string To)>();
        try
        {
            if (File.Exists(db)) Snapshot.Copy(db, Path.Combine(recovery, "pausewell.db"));
            // A crashed process can leave WAL data. Preserve it before replacing the database.
            foreach (var suffix in new[] { "-wal", "-shm" })
            {
                if (!File.Exists(db + suffix)) continue;
                var retained = Path.Combine(recovery, "original" + suffix);
                File.Move(db + suffix, retained);
                moved.Add((db + suffix, retained));
            }
            if (File.Exists(db)) File.Replace(staged, db, Path.Combine(recovery, "original-main.db"));
            else File.Move(staged, db);
        }
        catch
        {
            foreach (var move in moved.AsEnumerable().Reverse()) File.Move(move.To, move.From);
            throw;
        }
        finally { if (File.Exists(staged)) File.Delete(staged); }
        Console.WriteLine(JsonSerializer.Serialize(new { restored = true, recovery, state = Snapshot.Inspect(db) }));
    }
    else throw new ArgumentException("Unknown maintenance command.");
    return 0;
}
catch (Exception ex)
{
    // Do not echo SQLite payloads or medication names in logs.
    Console.Error.WriteLine(ex is ArgumentException or IOException or InvalidDataException ? ex.Message : ex.GetType().Name);
    return 1;
}

internal sealed record DataSummary(bool exists, int schema, int plans, int records, string theme, bool startupEnabled);

internal static class Snapshot
{
    public static DataSummary Inspect(string path)
    {
        using var connection = new Connection(path, false);
        if (connection.Scalar("PRAGMA integrity_check") != "ok") throw new InvalidDataException("数据库完整性检查失败。");
        var json = connection.Scalar("SELECT payload FROM app_state WHERE id=1");
        if (json == null) throw new InvalidDataException("数据库没有有效状态，已停止操作。");
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("SchemaVersion", out var schema) || schema.GetInt32() is < 1 or > 4)
            throw new InvalidDataException("数据库版本不受此安装包支持，未修改数据。");
        var state = JsonSerializer.Deserialize<AppState>(json, StateJson.Options) ?? throw new InvalidDataException("数据库内容无效。");
        return new(true, state.SchemaVersion, state.Plans.Count, state.Occurrences.Count, state.Preferences.Theme, state.Preferences.StartupEnabled);
    }

    public static void Copy(string source, string destination)
    {
        source = Path.GetFullPath(source); destination = Path.GetFullPath(destination);
        if (File.Exists(destination)) throw new IOException("备份目标已存在，未覆盖。");
        Inspect(source);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        // Reserve the destination exclusively. The caller always supplies a new name.
        using (new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        try
        {
            using (var input = new Connection(source, false))
            using (var output = new Connection(destination, true))
            {
                var backup = sqlite3_backup_init(output.Handle, "main", input.Handle, "main");
                if (backup == IntPtr.Zero) throw new IOException("无法建立数据库备份。");
                int result; int finished;
                try { result = sqlite3_backup_step(backup, -1); }
                finally { finished = sqlite3_backup_finish(backup); }
                if (result != 101 || finished != 0) throw new IOException("数据库忙或备份失败，未使用此备份。");
            }
            Inspect(destination);
        }
        catch { File.Delete(destination); throw; }
    }

    private sealed class Connection : IDisposable
    {
        public IntPtr Handle;
        public Connection(string path, bool writable)
        {
            if (!File.Exists(path)) throw new IOException("数据库文件不存在，未创建空数据库。");
            if (sqlite3_open_v2(path, out Handle, (writable ? 2 : 1) | 0x10000, IntPtr.Zero) != 0)
            { Dispose(); throw new IOException("无法打开数据库。"); }
            sqlite3_busy_timeout(Handle, 3000);
        }
        public string? Scalar(string sql)
        {
            if (sqlite3_prepare_v2(Handle, sql, -1, out var statement, IntPtr.Zero) != 0) throw new IOException("数据库结构无效，已停止操作。");
            try
            {
                var result = sqlite3_step(statement);
                if (result == 101) return null;
                if (result != 100) throw new IOException("无法读取数据库。");
                return Marshal.PtrToStringUTF8(sqlite3_column_text(statement, 0));
            }
            finally { sqlite3_finalize(statement); }
        }
        public void Dispose() { if (Handle != IntPtr.Zero) { sqlite3_close_v2(Handle); Handle = IntPtr.Zero; } }
    }
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out IntPtr db, int flags, IntPtr vfs);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_close_v2(IntPtr db);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_busy_timeout(IntPtr db, int ms);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_prepare_v2(IntPtr db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, int length, out IntPtr statement, IntPtr tail);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_step(IntPtr statement);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr sqlite3_column_text(IntPtr statement, int column);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_finalize(IntPtr statement);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr sqlite3_backup_init(IntPtr destination, [MarshalAs(UnmanagedType.LPUTF8Str)] string destinationName, IntPtr source, [MarshalAs(UnmanagedType.LPUTF8Str)] string sourceName);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_backup_step(IntPtr backup, int pages);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_backup_finish(IntPtr backup);
}
