using Microsoft.JSInterop;
using System.Text.Json;

namespace TPS.Nexus.Kanban.Web.Services;

public sealed class GridColumnPrefs
{
    public sealed record ColDef(string Id, string Title, double? FixedWidth = null, bool Resizable = true);

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    private readonly string _storageKey;

    public IReadOnlyList<ColDef> Defaults { get; }
    public List<string>               Order  { get; private set; }
    public Dictionary<string, double> Widths { get; private set; } = new();

    public GridColumnPrefs(string storageKey, IEnumerable<ColDef> defaults)
    {
        _storageKey = storageKey;
        Defaults    = defaults.ToList().AsReadOnly();
        Order       = Defaults.Select(c => c.Id).ToList();
    }

    public string? Width(string id)
    {
        if (Widths.TryGetValue(id, out var w)) return $"{w:F0}px";
        var def = Defaults.FirstOrDefault(c => c.Id == id);
        return def?.FixedWidth is double fw ? $"{fw:F0}px" : null;
    }

    public void SetWidth(string id, double px) => Widths[id] = px;

    public void Reorder(int from, int to)
    {
        if (from < 0 || from >= Order.Count || from == to) return;
        var item = Order[from];
        Order.RemoveAt(from);
        Order.Insert(Math.Min(to, Order.Count), item);
    }

    public void AppendToEnd(int from)
    {
        if (from < 0 || from >= Order.Count - 1) return;
        var item = Order[from];
        Order.RemoveAt(from);
        Order.Add(item);
    }

    public void Reset()
    {
        Order  = Defaults.Select(c => c.Id).ToList();
        Widths = new();
    }

    public async Task LoadAsync(IJSRuntime js)
    {
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", _storageKey);
            if (string.IsNullOrEmpty(json)) return;
            var saved = JsonSerializer.Deserialize<Payload>(json, JsonOpts);
            if (saved?.Order is { Count: > 0 })
            {
                var valid   = saved.Order.Where(id => Defaults.Any(c => c.Id == id)).ToList();
                var missing = Defaults.Select(c => c.Id).Except(valid);
                Order = valid.Concat(missing).ToList();
            }
            if (saved?.Widths is { Count: > 0 })
                Widths = saved.Widths;
        }
        catch { }
    }

    public async Task SaveAsync(IJSRuntime js)
    {
        try
        {
            var json = JsonSerializer.Serialize(new Payload { Order = Order, Widths = Widths });
            await js.InvokeVoidAsync("localStorage.setItem", _storageKey, json);
        }
        catch { }
    }

    private sealed class Payload
    {
        public List<string>?              Order  { get; set; }
        public Dictionary<string, double>? Widths { get; set; }
    }
}
