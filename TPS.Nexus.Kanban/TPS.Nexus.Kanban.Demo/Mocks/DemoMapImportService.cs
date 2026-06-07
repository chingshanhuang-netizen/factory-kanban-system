using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Interfaces;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Demo.Mocks;

public class DemoMapImportService : IMapImportService
{
    private readonly List<FactoryMap> _maps = new()
    {
        new()
        {
            Id         = 1,
            Name       = "廠區一樓平面圖",
            Version    = "v2",
            Floor      = "1F",
            Area       = "主廠區",
            FormatType = MapFormatType.Svg,
            FilePath   = "/maps/demo-floor.svg",
            CreatedAt  = DateTime.UtcNow.AddDays(-30),
        },
        new()
        {
            Id         = 2,
            Name       = "廠區二樓平面圖",
            Version    = "v1",
            Floor      = "2F",
            Area       = "主廠區",
            FormatType = MapFormatType.Svg,
            FilePath   = "/maps/demo-floor.svg",
            CreatedAt  = DateTime.UtcNow.AddDays(-15),
        },
    };

    public Task<FactoryMap> ImportAsync(Stream file, string fileName, MapFormatType format)
    {
        using var ms = new MemoryStream();
        file.CopyTo(ms);
        var bytes = ms.ToArray();
        var mime = format switch
        {
            MapFormatType.Png => "image/png",
            MapFormatType.Jpg => "image/jpeg",
            MapFormatType.Svg => "image/svg+xml",
            _                 => string.Empty,
        };
        var filePath = string.IsNullOrEmpty(mime)
            ? $"/maps/{System.IO.Path.GetFileName(fileName)}"
            : $"data:{mime};base64,{Convert.ToBase64String(bytes)}";

        var map = new FactoryMap
        {
            Id         = _maps.Count > 0 ? _maps.Max(m => m.Id) + 1 : 1,
            Name       = System.IO.Path.GetFileNameWithoutExtension(fileName),
            FormatType = format,
            FilePath   = filePath,
            CreatedAt  = DateTime.UtcNow,
        };
        _maps.Add(map);
        return Task.FromResult(map);
    }

    public Task<IEnumerable<FactoryMap>> GetAllAsync() =>
        Task.FromResult(_maps.AsEnumerable());

    public Task UpdateAsync(FactoryMap map)
    {
        var idx = _maps.FindIndex(m => m.Id == map.Id);
        if (idx >= 0) _maps[idx] = map;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(int mapId)
    {
        if (mapId <= 0) throw new ArgumentOutOfRangeException(nameof(mapId));
        _maps.RemoveAll(m => m.Id == mapId);
        return Task.CompletedTask;
    }

    public Task<bool> HasLayoutVersionsAsync(int mapId) =>
        Task.FromResult(false);
}
