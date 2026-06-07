using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Core.Interfaces;

public interface IAuditLogService
{
    Task LogDeleteAsync(string entityType, int entityId, string entityName,
                        string performedBy, string? details = null);
    Task<IEnumerable<AuditLog>> GetRecentLogsAsync(int count = 200);
}
