using System.Text.Json;
using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.DataSource;

public class SqlDataAdapter
{
    private readonly IDbConnectionFactory _factory;

    public SqlDataAdapter(IDbConnectionFactory factory) => _factory = factory;

    public Task<DataResult> FetchAsync(DataSourceConfig config)
    {
        var result = new DataResult();
        using var conn = _factory.CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = config.QueryOrPath
            ?? throw new InvalidOperationException("QueryOrPath is required for SQL source.");

        if (!string.IsNullOrEmpty(config.Parameters))
        {
            var paramDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(config.Parameters)
                ?? new Dictionary<string, JsonElement>();

            foreach (var (key, value) in paramDict)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = "@" + key;
                p.Value = value.ValueKind switch
                {
                    JsonValueKind.Number => value.TryGetInt64(out var l) ? (object)l : value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => value.GetString() ?? (object)DBNull.Value
                };
                cmd.Parameters.Add(p);
            }
        }

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            for (int i = 0; i < reader.FieldCount; i++)
                result.Fields[reader.GetName(i)] = reader.GetValue(i);
        }

        return Task.FromResult(result);
    }

    public Task<IEnumerable<DataResult>> FetchHistoryAsync(DataSourceConfig config, DateTime from, DateTime to)
    {
        var results = new List<DataResult>();
        using var conn = _factory.CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = config.QueryOrPath
            ?? throw new InvalidOperationException("QueryOrPath is required for SQL source.");

        void AddParam(string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
        }
        AddParam("@from", from);
        AddParam("@to", to);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var row = new DataResult();
            for (int i = 0; i < reader.FieldCount; i++)
                row.Fields[reader.GetName(i)] = reader.GetValue(i);
            results.Add(row);
        }

        return Task.FromResult<IEnumerable<DataResult>>(results);
    }
}
