using TPS.Nexus.Kanban.Core.Interfaces;
namespace TPS.Nexus.Kanban.Services.Map;
public class MapImportService : IMapImportService
{
    public Task<TPS.Nexus.Kanban.Core.Models.FactoryMap> ImportAsync(System.IO.Stream file, string fileName, TPS.Nexus.Kanban.Core.Enums.MapFormatType format) => throw new NotImplementedException();
    public Task<IEnumerable<TPS.Nexus.Kanban.Core.Models.FactoryMap>> GetAllAsync() => throw new NotImplementedException();
    public Task DeleteAsync(int mapId) => throw new NotImplementedException();
}
