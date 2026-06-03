using Dapper;
using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Interfaces;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.Layout;

public class LayoutService : ILayoutService
{
    private readonly IDbConnectionFactory _db;

    public LayoutService(IDbConnectionFactory db) => _db = db;

    public async Task<LayoutVersion> SaveDraftAsync(int factoryMapId, string layoutJson, string createdBy)
    {
        using var conn = _db.CreateConnection();
        var maxVersion = await conn.ExecuteScalarAsync<int>(
            "SELECT COALESCE(MAX(VersionNo), 0) FROM kanban_layout_versions WHERE FactoryMapId=@FactoryMapId",
            new { FactoryMapId = factoryMapId });

        var version = new LayoutVersion
        {
            FactoryMapId = factoryMapId,
            VersionNo    = maxVersion + 1,
            Status       = LayoutStatus.Draft,
            CreatedBy    = createdBy,
            LayoutJson   = layoutJson
        };

        version.Id = await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO kanban_layout_versions (FactoryMapId, VersionNo, Status, CreatedBy, PublishedAt, LayoutJson)
            VALUES (@FactoryMapId, @VersionNo, @Status, @CreatedBy, @PublishedAt, @LayoutJson);
            SELECT LAST_INSERT_ID();
            """, version);

        return version;
    }

    public async Task<LayoutVersion> PublishAsync(int draftId)
    {
        using var conn = _db.CreateConnection();
        var draft = await conn.QueryFirstOrDefaultAsync<LayoutVersion>(
            "SELECT * FROM kanban_layout_versions WHERE Id=@Id", new { Id = draftId })
            ?? throw new InvalidOperationException($"Layout version {draftId} not found.");

        if (draft.Status != LayoutStatus.Draft)
            throw new InvalidOperationException($"Only Draft versions can be published. Current: {draft.Status}");

        await conn.ExecuteAsync(
            "UPDATE kanban_layout_versions SET Status=@Archived WHERE FactoryMapId=@MapId AND Status=@Published",
            new { Archived = (byte)LayoutStatus.Archived, MapId = draft.FactoryMapId, Published = (byte)LayoutStatus.Published });

        draft.Status      = LayoutStatus.Published;
        draft.PublishedAt = DateTime.UtcNow;
        await conn.ExecuteAsync(
            "UPDATE kanban_layout_versions SET Status=@Status, PublishedAt=@PublishedAt WHERE Id=@Id", draft);

        return draft;
    }

    public async Task<IEnumerable<LayoutVersion>> GetVersionHistoryAsync(int mapId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<LayoutVersion>(
            "SELECT * FROM kanban_layout_versions WHERE FactoryMapId=@MapId ORDER BY VersionNo DESC",
            new { MapId = mapId });
    }

    public async Task<LayoutVersion?> GetPublishedVersionAsync(int mapId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<LayoutVersion>(
            "SELECT * FROM kanban_layout_versions WHERE FactoryMapId=@MapId AND Status=@Status",
            new { MapId = mapId, Status = (byte)LayoutStatus.Published });
    }

    public async Task RollbackAsync(int versionId)
    {
        using var conn = _db.CreateConnection();
        var target = await conn.QueryFirstOrDefaultAsync<LayoutVersion>(
            "SELECT * FROM kanban_layout_versions WHERE Id=@Id", new { Id = versionId })
            ?? throw new InvalidOperationException($"Layout version {versionId} not found.");

        await conn.ExecuteAsync(
            "UPDATE kanban_layout_versions SET Status=@Archived WHERE FactoryMapId=@MapId AND Status=@Published",
            new { Archived = (byte)LayoutStatus.Archived, MapId = target.FactoryMapId, Published = (byte)LayoutStatus.Published });

        await conn.ExecuteAsync(
            "UPDATE kanban_layout_versions SET Status=@Published, PublishedAt=@Now WHERE Id=@Id",
            new { Published = (byte)LayoutStatus.Published, Now = DateTime.UtcNow, Id = versionId });
    }
}
