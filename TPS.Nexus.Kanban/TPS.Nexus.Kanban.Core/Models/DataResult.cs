namespace TPS.Nexus.Kanban.Core.Models;

public class DataResult
{
    public Dictionary<string, object?> Fields { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public T? Get<T>(string key)
    {
        if (Fields.TryGetValue(key, out var val) && val is T typed)
            return typed;
        return default;
    }
}
