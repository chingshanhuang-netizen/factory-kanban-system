using System.Text.Json;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.DataSource;

public class JsonDataAdapter
{
    public async Task<DataResult> FetchAsync(DataSourceConfig config)
    {
        var result = new DataResult();
        var path = config.FilePath
            ?? throw new InvalidOperationException(
                $"DataSourceConfig '{config.Name}' (Id={config.Id}): FilePath is required for JSON source.");

        // DA-2: IOException (FileNotFoundException, etc.) wrapped with config context.
        string json;
        try { json = await File.ReadAllTextAsync(path); }
        catch (IOException ex)
        {
            throw new InvalidOperationException(
                $"DataSourceConfig '{config.Name}' (Id={config.Id}): Cannot read JSON file at '{path}'. Inner: {ex.Message}", ex);
        }

        // DA-3: malformed JSON wrapped with config context so callers know which config is broken.
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"DataSourceConfig '{config.Name}' (Id={config.Id}): File at '{path}' contains invalid JSON. Inner: {ex.Message}", ex);
        }

        using (doc)
        {
            var target = string.IsNullOrEmpty(config.QueryOrPath)
                ? doc.RootElement
                : NavigatePath(doc.RootElement, config.QueryOrPath, config.Name);

            if (target.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in target.EnumerateObject())
                    result.Fields[prop.Name] = ExtractValue(prop.Value);
            }
        }

        return result;
    }

    /// <remarks>
    /// DS-3: JSON files are static snapshots without per-record timestamps. This method returns
    /// all array items regardless of <paramref name="from"/> and <paramref name="to"/>. If
    /// time-range filtering is required, include a timestamp field in each JSON object.
    /// </remarks>
    public async Task<IEnumerable<DataResult>> FetchHistoryAsync(DataSourceConfig config, DateTime from, DateTime to)
    {
        var results = new List<DataResult>();
        var path = config.FilePath
            ?? throw new InvalidOperationException(
                $"DataSourceConfig '{config.Name}' (Id={config.Id}): FilePath is required for JSON source.");

        string json;
        try { json = await File.ReadAllTextAsync(path); }
        catch (IOException ex)
        {
            throw new InvalidOperationException(
                $"DataSourceConfig '{config.Name}' (Id={config.Id}): Cannot read JSON file at '{path}'. Inner: {ex.Message}", ex);
        }

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"DataSourceConfig '{config.Name}' (Id={config.Id}): File at '{path}' contains invalid JSON. Inner: {ex.Message}", ex);
        }

        using (doc)
        {
            var target = string.IsNullOrEmpty(config.QueryOrPath)
                ? doc.RootElement
                : NavigatePath(doc.RootElement, config.QueryOrPath, config.Name);

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
        }

        return results;
    }

    // DS-5: throw if a path segment is not found so callers know the configuration is wrong,
    // rather than silently returning the last-matched parent node with unrelated data.
    private static JsonElement NavigatePath(JsonElement root, string path, string configName)
    {
        var segments = path.Trim('/').Split('/');
        var current = root;
        foreach (var seg in segments)
        {
            if (!current.TryGetProperty(seg, out var next))
                throw new KeyNotFoundException(
                    $"DataSourceConfig '{configName}': JSON path segment '{seg}' not found " +
                    $"while navigating '{path}'. Check QueryOrPath configuration.");
            current = next;
        }
        return current;
    }

    private static object? ExtractValue(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
        JsonValueKind.True   => true,
        JsonValueKind.False  => false,
        JsonValueKind.Null   => null,
        _                    => el.GetString()
    };
}
