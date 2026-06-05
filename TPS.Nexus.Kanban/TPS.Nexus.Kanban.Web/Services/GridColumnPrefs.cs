using Microsoft.JSInterop;

namespace TPS.Nexus.Kanban.Web.Services;

public sealed class GridColumnPrefs
{
    public sealed record ColDef(string Id, string Title, double? FixedWidth = null, bool Resizable = true);

    private readonly string           _gridId;
    private readonly UserPrefsService _prefs;

    public IReadOnlyList<ColDef>      Defaults { get; }
    public List<string>               Order    { get; private set; }
    public Dictionary<string, double> Widths   { get; private set; } = new();

    public GridColumnPrefs(string gridId, IEnumerable<ColDef> defaults, UserPrefsService prefs)
    {
        _gridId  = gridId;
        _prefs   = prefs;
        Defaults = defaults.ToList().AsReadOnly();
        Order    = Defaults.Select(c => c.Id).ToList();
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
        await _prefs.EnsureLoadedAsync(js);
        var saved = _prefs.GetGridPrefs(_gridId);
        if (saved.Order is { Count: > 0 })
        {
            var valid   = saved.Order.Where(id => Defaults.Any(c => c.Id == id)).ToList();
            var missing = Defaults.Select(c => c.Id).Except(valid);
            Order = valid.Concat(missing).ToList();
        }
        if (saved.Widths is { Count: > 0 })
            Widths = saved.Widths;
    }

    public async Task SaveAsync(IJSRuntime js)
    {
        var g = _prefs.GetGridPrefs(_gridId);
        g.Order  = Order;
        g.Widths = Widths;
        await _prefs.SaveAsync(js);
    }
}
