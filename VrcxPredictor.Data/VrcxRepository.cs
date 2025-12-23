using Dapper;
using Microsoft.Data.Sqlite;
using System.IO;

namespace VrcxPredictor.Data;

public sealed class VrcxRepository
{
    public string DbPath { get; set; }

    public VrcxRepository(string dbPath) => DbPath = dbPath;

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(DbPath))
            throw new FileNotFoundException("未设置数据库路径。请先选择 VRCX.sqlite3。");
        if (!File.Exists(DbPath))
            throw new FileNotFoundException($"数据库不存在：{DbPath}");
    }

    public async Task<IReadOnlyList<string>> ListTablesAsync()
    {
        Validate();

        await using var conn = new SqliteConnection($"Data Source={DbPath};Mode=ReadOnly;");
        var sql = @"
SELECT name FROM sqlite_master
WHERE type='table' AND name LIKE 'usr%_feed_online_offline'
ORDER BY name;";
        var rows = await conn.QueryAsync<string>(sql);
        return rows.AsList();
    }

    public async Task<IReadOnlyList<string>> ListDistinctDisplayNamesAsync(string table, int max = 200)
    {
        Validate();
        table = EnsureSafeTableName(table);

        await using var conn = new SqliteConnection($"Data Source={DbPath};Mode=ReadOnly;");
        var sql = $@"
SELECT DISTINCT display_name
FROM {table}
WHERE display_name IS NOT NULL AND display_name <> ''
ORDER BY display_name
LIMIT @max;";
        var rows = await conn.QueryAsync<string>(sql, new { max });
        return rows.AsList();
    }

    public async Task<IReadOnlyList<(string Type, string CreatedAt)>> ReadUserEventsRawAsync(string table, string displayName)
    {
        Validate();
        table = EnsureSafeTableName(table);

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("显示名称不能为空。");

        await using var conn = new SqliteConnection($"Data Source={DbPath};Mode=ReadOnly;");
        var sql = $@"
SELECT type as Type, created_at as CreatedAt
FROM {table}
WHERE display_name = @name COLLATE NOCASE
ORDER BY created_at;";

        var rows = await conn.QueryAsync<(string Type, string CreatedAt)>(sql, new { name = displayName });
        return rows.AsList();
    }

public async Task<IReadOnlyList<string>> SearchDisplayNamesAsync(string table, string keyword, int max = 50)
{
    Validate();
    table = EnsureSafeTableName(table);

    if (string.IsNullOrWhiteSpace(keyword))
        return Array.Empty<string>();

    await using var conn = new SqliteConnection($"Data Source={DbPath};Mode=ReadOnly;");
    var sql = $@"
SELECT DISTINCT display_name
FROM {table}
WHERE display_name IS NOT NULL AND display_name <> ''
  AND display_name LIKE @kw ESCAPE '\\'
ORDER BY display_name
LIMIT @max;";
    var kw = "%" + EscapeLike(keyword.Trim()) + "%";
    var rows = await conn.QueryAsync<string>(sql, new { kw, max });
    return rows.AsList();
}

private static string EscapeLike(string s)
    => s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    public async Task<IReadOnlyList<string>> ReadAllOnlineCreatedAtAsync(string table)
    {
        Validate();
        table = EnsureSafeTableName(table);

        await using var conn = new SqliteConnection($"Data Source={DbPath};Mode=ReadOnly;");
        var sql = $@"
SELECT created_at
FROM {table}
WHERE type = 'Online'
ORDER BY created_at;";

        var rows = await conn.QueryAsync<string>(sql);
        return rows.AsList();
    }

    private static string EnsureSafeTableName(string table)
    {
        if (string.IsNullOrWhiteSpace(table))
            throw new ArgumentException("未选择数据表。");

        if (!table.StartsWith("usr", StringComparison.OrdinalIgnoreCase) ||
            !table.EndsWith("_feed_online_offline", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("非法表名（不在允许范围）。");

        foreach (char c in table)
        {
            if (!(char.IsLetterOrDigit(c) || c == '_'))
                throw new ArgumentException("非法表名（包含不允许字符）。");
        }

        return table;
    }
}
