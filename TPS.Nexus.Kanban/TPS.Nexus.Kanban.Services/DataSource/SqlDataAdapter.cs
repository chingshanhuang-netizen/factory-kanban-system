using System.Data.Common;
using System.Text.Json;
using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.DataSource;

public class SqlDataAdapter
{
    private readonly IDbConnectionFactory _factory;

    public SqlDataAdapter(IDbConnectionFactory factory) => _factory = factory;

    public async Task<DataResult> FetchAsync(DataSourceConfig config)
    {
        var result = new DataResult();
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = config.QueryOrPath
            ?? throw new InvalidOperationException(
                $"DataSourceConfig '{config.Name}' (Id={config.Id}): QueryOrPath is required for SQL source.");

        if (!string.IsNullOrEmpty(config.Parameters))
        {
            // DS-4: wrap JSON parse errors with context so callers know which config is broken
            Dictionary<string, JsonElement> paramDict;
            try
            {
                paramDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(config.Parameters)
                    ?? new Dictionary<string, JsonElement>();
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"DataSourceConfig '{config.Name}' (Id={config.Id}): Parameters field contains invalid JSON. " +
                    $"Expected a JSON object, e.g. {{\"key\": value}}. Inner: {ex.Message}", ex);
            }

            foreach (var (key, value) in paramDict)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = "@" + key;
                p.Value = value.ValueKind switch
                {
                    JsonValueKind.Number => value.TryGetInt64(out var l) ? (object)l : value.GetDouble(),
                    JsonValueKind.True   => true,
                    JsonValueKind.False  => false,
                    _ => value.GetString() ?? (object)DBNull.Value
                };
                cmd.Parameters.Add(p);
            }
        }

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            for (int i = 0; i < reader.FieldCount; i++)
                result.Fields[reader.GetName(i)] = reader.GetValue(i);
        }

        return result;
    }

    /// <remarks>
    /// DS-8: The SQL query in <see cref="DataSourceConfig.QueryOrPath"/> MUST use
    /// <c>@from</c> and <c>@to</c> as the time-range parameter names, e.g.:
    /// <code>SELECT * FROM sensor_log WHERE recorded_at BETWEEN @from AND @to</code>
    /// </remarks>
    public async Task<IEnumerable<DataResult>> FetchHistoryAsync(DataSourceConfig config, DateTime from, DateTime to)
    {
        var results = new List<DataResult>();
        await using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = config.QueryOrPath
            ?? throw new InvalidOperationException(
                $"DataSourceConfig '{config.Name}' (Id={config.Id}): QueryOrPath is required for SQL source.");

        void AddParam(string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
        }
        AddParam("@from", from);
        AddParam("@to", to);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new DataResult();
            for (int i = 0; i < reader.FieldCount; i++)
                row.Fields[reader.GetName(i)] = reader.GetValue(i);
            results.Add(row);
        }

        return results;
    }
}
