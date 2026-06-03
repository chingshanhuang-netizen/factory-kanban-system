using System.Text.Json;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.DataSource;

public class JsonDataAdapter
{
    public async Task<DataResult> FetchAsync(DataSourceConfig config)
    {
        var result = new DataResult();
        var path = config.FilePath
            ?? throw new InvalidOperationException("FilePath is required for JSON source.");

        var json = await File.ReadAllTextAsync(path);
        using var doc = JsonDocument.Parse(json);

        var target = string.IsNullOrEmpty(config.QueryOrPath)
            ? doc.RootElement
            : NavigatePath(doc.RootElement, config.QueryOrPath);

        if (target.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in target.EnumerateObject())
                result.Fields[prop.Name] = ExtractValue(prop.Value);
        }

        return result;
    }

    public async Task<IEnumerable<DataResult>> FetchHistoryAsync(DataSourceConfig config, DateTime from, DateTime to)
    {
        var results = new List<DataResult>();
        var path = config.FilePath
            ?? throw new InvalidOperationException("FilePath is required for JSON source.");

        var json = await File.ReadAllTextAsync(path);
        using var doc = JsonDocument.Parse(json);

        var target = string.IsNullOrEmpty(config.QueryOrPath)
            ? doc.RootElement
            : NavigatePath(doc.RootElement, config.QueryOrPath);

        if (target.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in target.EnumerateArray())
            {
                var row = new DataResult();
                if (item.ValueKind == JsonValueKind.Object)
                    foreach (var prop in item.EnumerateObject())
                        row.Fields[prop.Name] = ExtractValue(prop.Value);
                results.Add(row);
            }
        }

        return results;
    }

    private static JsonElement NavigatePath(JsonElement root, string path)
    {
        var segments = path.Trim('/').Split('/');
        var current = root;
        foreach (var seg in segments)
            if (current.TryGetProperty(seg, out var next))
                current = next;
        return current;
    }

    private static object? ExtractValue(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => el.GetString()
    };
}
