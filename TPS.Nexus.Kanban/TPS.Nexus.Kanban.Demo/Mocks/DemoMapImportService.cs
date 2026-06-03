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
            FormatType = MapFormatType.Svg,
            FilePath   = "/maps/demo-floor.svg",   // served from wwwroot/maps/
            CreatedAt  = DateTime.UtcNow.AddDays(-30),
        },
    };

    public Task<FactoryMap> ImportAsync(Stream file, string fileName, MapFormatType format) =>
        throw new NotSupportedException("Demo 模式不支援地圖上傳，請使用真實後端。");

    public Task<IEnumerable<FactoryMap>> GetAllAsync() =>
        Task.FromResult(_maps.AsEnumerable());

    public Task DeleteAsync(int mapId)
    {
        if (mapId <= 0) throw new ArgumentOutOfRangeException(nameof(mapId));
        _maps.RemoveAll(m => m.Id == mapId);
        return Task.CompletedTask;
    }
}
