using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Core.Interfaces;

public interface ILayoutService
{
    Task<LayoutVersion> SaveDraftAsync(int factoryMapId, string layoutJson, string createdBy);
    Task<LayoutVersion> PublishAsync(int draftId);
    Task<IEnumerable<LayoutVersion>> GetVersionHistoryAsync(int mapId);
    Task<LayoutVersion?> GetPublishedVersionAsync(int mapId);
    Task RollbackAsync(int versionId);
}
