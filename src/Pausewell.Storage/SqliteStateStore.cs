using System.Runtime.InteropServices;
using System.Text.Json;
using Pausewell.Core;

namespace Pausewell.Storage;

// Windows ships winsqlite3. No downloaded native database binary is required.
public sealed class SqliteStateStore : IStateStore, IDisposable
{
    private IntPtr _db;
    public string FilePath { get; }
    public SqliteStateStore(string path)
    {
        FilePath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var code = sqlite3_open_v2(FilePath, out _db, 0x00000002 | 0x00000004 | 0x00010000, IntPtr.Zero);
        if (code != 0) { var message = Error(); Dispose(); throw new IOException(message); }
        sqlite3_busy_timeout(_db, 3000);
        Exec("PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; CREATE TABLE IF NOT EXISTS app_state(id INTEGER PRIMARY KEY CHECK(id=1), payload TEXT NOT NULL);");
    }

    private string Error() => "本地记录保存失败：" + Marshal.PtrToStringUTF8(sqlite3_errmsg(_db));
    private void Check(int result) { if (result != 0) throw new IOException(Error()); }
    private void Exec(string sql)
    {
        var code = sqlite3_exec(_db, sql, IntPtr.Zero, IntPtr.Zero, out var error);
        if (error != IntPtr.Zero) sqlite3_free(error);
        Check(code);
    }

    public AppState? Load()
    {
        Check(sqlite3_prepare_v2(_db, "SELECT payload FROM app_state WHERE id=1", -1, out var stmt, IntPtr.Zero));
        try
        {
            var code = sqlite3_step(stmt);
            if (code == 101) return null;
            if (code != 100) throw new IOException(Error());
            return JsonSerializer.Deserialize<AppState>(Marshal.PtrToStringUTF8(sqlite3_column_text(stmt, 0))!, StateJson.Options)
                ?? throw new InvalidDataException("本地记录内容为空，未覆盖原文件。");
        }
        finally { sqlite3_finalize(stmt); }
    }

    public void Save(AppState state)
    {
        var json = JsonSerializer.Serialize(state, StateJson.Options);
        Exec("BEGIN IMMEDIATE;");
        try
        {
            Check(sqlite3_prepare_v2(_db, "INSERT INTO app_state(id,payload) VALUES(1,?1) ON CONFLICT(id) DO UPDATE SET payload=excluded.payload", -1, out var stmt, IntPtr.Zero));
            try
            {
                Check(sqlite3_bind_text(stmt, 1, json, -1, new IntPtr(-1)));
                if (sqlite3_step(stmt) != 101) throw new IOException(Error());
            }
            finally { sqlite3_finalize(stmt); }
            Exec("COMMIT;");
        }
        catch { try { Exec("ROLLBACK;"); } catch { /* Preserve original failure. */ } throw; }
    }

    public string IntegrityCheck()
    {
        Check(sqlite3_prepare_v2(_db, "PRAGMA integrity_check", -1, out var stmt, IntPtr.Zero));
        try { if (sqlite3_step(stmt) != 100) throw new IOException(Error()); return Marshal.PtrToStringUTF8(sqlite3_column_text(stmt, 0))!; }
        finally { sqlite3_finalize(stmt); }
    }
    public string Backup()
    {
        var target = FilePath + ".before-schema4-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmssfff") + ".db";
        Exec("VACUUM INTO '" + target.Replace("'", "''") + "';");
        return target;
    }
    public void Dispose() { if (_db != IntPtr.Zero) { sqlite3_close_v2(_db); _db = IntPtr.Zero; } }

    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out IntPtr db, int flags, IntPtr vfs);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_close_v2(IntPtr db);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr sqlite3_errmsg(IntPtr db);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_busy_timeout(IntPtr db, int ms);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_exec(IntPtr db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, IntPtr callback, IntPtr arg, out IntPtr error);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern void sqlite3_free(IntPtr value);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_prepare_v2(IntPtr db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, int length, out IntPtr stmt, IntPtr tail);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_bind_text(IntPtr stmt, int index, [MarshalAs(UnmanagedType.LPUTF8Str)] string value, int length, IntPtr destructor);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_step(IntPtr stmt);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr sqlite3_column_text(IntPtr stmt, int column);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_finalize(IntPtr stmt);
}
