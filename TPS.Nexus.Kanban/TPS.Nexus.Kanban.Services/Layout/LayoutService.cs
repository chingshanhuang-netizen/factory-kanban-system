using TPS.Nexus.Kanban.Core.Interfaces;
using TPS.Nexus.Kanban.Core.Models;
namespace TPS.Nexus.Kanban.Services.Layout;
public class LayoutService : ILayoutService
{
    public Task<LayoutVersion> SaveDraftAsync(int factoryMapId, string layoutJson, string createdBy) => throw new NotImplementedException();
    public Task<LayoutVersion> PublishAsync(int draftId) => throw new NotImplementedException();
    public Task<IEnumerable<LayoutVersion>> GetVersionHistoryAsync(int mapId) => throw new NotImplementedException();
    public Task<LayoutVersion?> GetPublishedVersionAsync(int mapId) => throw new NotImplementedException();
    public Task RollbackAsync(int versionId) => throw new NotImplementedException();
}
