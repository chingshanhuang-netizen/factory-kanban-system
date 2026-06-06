using MySqlConnector;

// Set via environment variables or command-line args before running:
//   $env:DB_HOST = "..."  $env:DB_USER = "..."  $env:DB_PWD = "..."  $env:DB_NAME = "..."
var host   = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
var user   = Environment.GetEnvironmentVariable("DB_USER") ?? "root";
var pwd    = Environment.GetEnvironmentVariable("DB_PWD")  ?? "";
var dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? "tps_nexus";

var connStr = $"Server={host};Port=3306;Database={dbName};Uid={user};Pwd={pwd};CharSet=utf8mb4;ConnectionTimeout=15;";
Console.WriteLine($"連線至 {host} / {dbName} ...");

// Run a targeted fix for the kanban_alarm_rules table (Condition is a reserved word)
const string fixSql = """
    CREATE TABLE IF NOT EXISTS kanban_alarm_rules (
        Id                  INT             NOT NULL AUTO_INCREMENT,
        EquipmentId         INT             NOT NULL,
        DataSourceConfigId  INT             NOT NULL,
        FieldName           VARCHAR(100)    NOT NULL DEFAULT 'value',
        `Condition`         VARCHAR(10)     NOT NULL,
        Threshold           DOUBLE          NOT NULL,
        AlarmLevel          TINYINT         NOT NULL,
        Message             VARCHAR(500)    NULL,
        IsEnabled           TINYINT(1)      NOT NULL DEFAULT 1,
        PRIMARY KEY (Id),
        KEY idx_equipment (EquipmentId),
        CONSTRAINT fk_ar_equipment FOREIGN KEY (EquipmentId)
            REFERENCES kanban_equipment (Id) ON DELETE CASCADE ON UPDATE CASCADE,
        CONSTRAINT fk_ar_datasource FOREIGN KEY (DataSourceConfigId)
            REFERENCES kanban_datasource_configs (Id) ON DELETE CASCADE ON UPDATE CASCADE
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='警報規則'
    """;

var sqlFile = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        "docs", "superpowers", "specs", "2026-06-07-kanban-mysql-schema.sql"));

if (!File.Exists(sqlFile))
{
    Console.Error.WriteLine($"SQL file not found: {sqlFile}");
    return 1;
}

var rawSql = await File.ReadAllTextAsync(sqlFile);

// Split into individual statements; skip empty and comment-only blocks
var statements = rawSql
    .Split(';')
    .Select(s =>
    {
        // Strip inline comments from each line, then trim
        var lines = s.Split('\n')
            .Select(l =>
            {
                var idx = l.IndexOf("--", StringComparison.Ordinal);
                return idx >= 0 ? l[..idx] : l;
            })
            .Where(l => !string.IsNullOrWhiteSpace(l));
        return string.Join('\n', lines).Trim();
    })
    .Where(s => !string.IsNullOrWhiteSpace(s))
    .ToList();


await using var conn = new MySqlConnection(connStr);
try
{
    await conn.OpenAsync();
    Console.WriteLine("連線成功。");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"連線失敗：{ex.Message}");
    return 1;
}

// Execute the targeted fix first (kanban_alarm_rules with backtick-quoted Condition)
{
    await using var cmd = new MySqlCommand(fixSql, conn);
    await cmd.ExecuteNonQueryAsync();
    Console.WriteLine("  OK  kanban_alarm_rules (fix applied)");
}

int ok = 0, fail = 0;
foreach (var stmt in statements)
{
    var preview = stmt.Length > 60 ? stmt[..60].Replace('\n', ' ') + "..." : stmt.Replace('\n', ' ');
    try
    {
        await using var cmd = new MySqlCommand(stmt, conn);
        await cmd.ExecuteNonQueryAsync();
        Console.WriteLine($"  OK  {preview}");
        ok++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ERR {preview}");
        Console.WriteLine($"      {ex.Message}");
        fail++;
    }
}

Console.WriteLine();
Console.WriteLine($"完成：{ok} 成功，{fail} 失敗。");

// Verify: list all kanban_ tables
Console.WriteLine("\n── 資料庫現有 kanban_ 資料表 ──");
await using var verifyCmd = new MySqlCommand(
    "SELECT TABLE_NAME, TABLE_ROWS, CREATE_TIME FROM information_schema.TABLES " +
    $"WHERE TABLE_SCHEMA = '{dbName}' AND TABLE_NAME LIKE 'kanban_%' ORDER BY TABLE_NAME;",
    conn);
await using var reader = await verifyCmd.ExecuteReaderAsync();
while (await reader.ReadAsync())
    Console.WriteLine($"  {reader.GetString(0),-42} rows≈{reader.GetValue(1),-6} created={reader.GetValue(2)}");

return fail == 0 ? 0 : 1;
