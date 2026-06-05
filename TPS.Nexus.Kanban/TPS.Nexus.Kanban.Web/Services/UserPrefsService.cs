using Microsoft.JSInterop;
using System.Text.Json;

namespace TPS.Nexus.Kanban.Web.Services;

/// <summary>
/// Single-JSON user preferences: selected map + all grid column order/widths.
/// localStorage key: kanban:user-prefs
/// </summary>
public sealed class UserPrefsService
{
    private const string StorageKey = "kanban:user-prefs";
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public int? SelectedMapId { get; private set; }

    private Dictionary<string, GridPrefs> _grids = new();
    private bool _loaded;

    public async Task EnsureLoadedAsync(IJSRuntime js)
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            var raw = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (string.IsNullOrEmpty(raw)) return;
            var saved = JsonSerializer.Deserialize<Payload>(raw, JsonOpts);
            if (saved == null) return;
            SelectedMapId = saved.SelectedMapId;
            if (saved.Grids != null) _grids = saved.Grids;
        }
        catch { }
    }

    public void SetSelectedMapId(int? id) => SelectedMapId = id;

    internal GridPrefs GetGridPrefs(string gridId)
    {
        if (!_grids.TryGetValue(gridId, out var p))
            _grids[gridId] = p = new GridPrefs();
        return p;
    }

    public async Task SaveAsync(IJSRuntime js)
    {
        try
        {
            var json = JsonSerializer.Serialize(
                new Payload { SelectedMapId = SelectedMapId, Grids = _grids });
            await js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }
        catch { }
    }

    public sealed class GridPrefs
    {
        public List<string>?               Order  { get; set; }
        public Dictionary<string, double>? Widths { get; set; }
    }

    private sealed class Payload
    {
        public int?                              SelectedMapId { get; set; }
        public Dictionary<string, GridPrefs>?   Grids         { get; set; }
    }
}
