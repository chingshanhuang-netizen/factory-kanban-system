using Dapper;
using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Core.Interfaces;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.Audit;

public class AuditLogService : IAuditLogService
{
    private readonly IDbConnectionFactory _db;

    public AuditLogService(IDbConnectionFactory db) => _db = db;

    public async Task LogDeleteAsync(string entityType, int entityId, string entityName,
                                     string performedBy, string? details = null)
    {
        await using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            """
            INSERT INTO kanban_audit_logs (EntityType, EntityId, EntityName, Action, PerformedBy, Details)
            VALUES (@EntityType, @EntityId, @EntityName, 'DELETE', @PerformedBy, @Details)
            """,
            new { EntityType = entityType, EntityId = entityId, EntityName = entityName,
                  PerformedBy = performedBy, Details = details });
    }

    public async Task<IEnumerable<AuditLog>> GetRecentLogsAsync(int count = 200)
    {
        await using var conn = _db.CreateConnection();
        return await conn.QueryAsync<AuditLog>(
            "SELECT * FROM kanban_audit_logs ORDER BY PerformedAt DESC LIMIT @Count",
            new { Count = count });
    }
}
