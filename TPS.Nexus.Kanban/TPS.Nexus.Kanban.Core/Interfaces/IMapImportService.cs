using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Core.Interfaces;

public interface IMapImportService
{
    Task<FactoryMap> ImportAsync(Stream file, string fileName, MapFormatType format);
    Task<IEnumerable<FactoryMap>> GetAllAsync();
    Task DeleteAsync(int mapId);
}
