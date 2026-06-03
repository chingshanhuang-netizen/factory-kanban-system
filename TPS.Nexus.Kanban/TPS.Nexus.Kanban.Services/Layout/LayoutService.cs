using System.Data;
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
        // B-2: Guard non-positive IDs before hitting the DB
        if (factoryMapId <= 0)
            throw new ArgumentOutOfRangeException(nameof(factoryMapId),
                $"factoryMapId must be a positive integer, got {factoryMapId}.");

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        // B-1 fix: wrap MAX + INSERT in a transaction with SELECT FOR UPDATE to prevent
        // concurrent requests from obtaining the same VersionNo for the same map.
        // LA-2: BeginTransactionAsync lets the provider use its async implementation.
        await using var tx = await conn.BeginTransactionAsync(IsolationLevel.RepeatableRead);

        // B-2 fix: verify the map exists before creating a version for it.
        var mapExists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM kanban_factory_maps WHERE Id=@Id",
            new { Id = factoryMapId }, transaction: tx);

        if (mapExists == 0)
            throw new InvalidOperationException(
                $"FactoryMap {factoryMapId} does not exist. Cannot create a version for a non-existent map.");

        // FOR UPDATE locks matching rows so concurrent transactions serialise on this map's versions.
        var maxVersion = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COALESCE(MAX(VersionNo), 0)
            FROM kanban_layout_versions
            WHERE FactoryMapId=@FactoryMapId
            FOR UPDATE
            """,
            new { FactoryMapId = factoryMapId }, transaction: tx);

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
            """, version, transaction: tx);

        // LA-1: CommitAsync lets the provider use its async commit path.
        await tx.CommitAsync();
        return version;
    }

    public async Task<LayoutVersion> PublishAsync(int draftId)
    {
        // B-4: Reject obviously invalid input early with a clear message.
        if (draftId <= 0)
            throw new ArgumentOutOfRangeException(nameof(draftId),
                $"draftId must be a positive integer, got {draftId}.");

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        // B-3 fix: wrap archive + publish in a single transaction so the two UPDATEs are atomic.
        // RepeatableRead prevents a concurrent Publish from archiving the same row between our two statements.
        await using var tx = await conn.BeginTransactionAsync(IsolationLevel.RepeatableRead);

        var draft = await conn.QueryFirstOrDefaultAsync<LayoutVersion>(
            "SELECT * FROM kanban_layout_versions WHERE Id=@Id FOR UPDATE",
            new { Id = draftId }, transaction: tx)
            ?? throw new InvalidOperationException($"Layout version {draftId} not found.");

        if (draft.Status != LayoutStatus.Draft)
            throw new InvalidOperationException(
                $"Only Draft versions can be published. Version {draftId} is currently '{draft.Status}'.");

        // Archive any existing Published version for this map before promoting the draft.
        await conn.ExecuteAsync(
            """
            UPDATE kanban_layout_versions
            SET Status=@Archived
            WHERE FactoryMapId=@MapId AND Status=@Published
            """,
            new { Archived = (byte)LayoutStatus.Archived, MapId = draft.FactoryMapId, Published = (byte)LayoutStatus.Published },
            transaction: tx);

        draft.Status      = LayoutStatus.Published;
        draft.PublishedAt = DateTime.UtcNow;
        await conn.ExecuteAsync(
            "UPDATE kanban_layout_versions SET Status=@Status, PublishedAt=@PublishedAt WHERE Id=@Id",
            draft, transaction: tx);

        await tx.CommitAsync();
        return draft;
    }

    public async Task<IEnumerable<LayoutVersion>> GetVersionHistoryAsync(int mapId)
    {
        // LA-3: a zero mapId (e.g. un-initialised Blazor route parameter) would silently
        // return an empty list, masking the caller bug.
        if (mapId <= 0)
            throw new ArgumentOutOfRangeException(nameof(mapId),
                $"mapId must be positive, got {mapId}.");

        await using var conn = _db.CreateConnection();
        return await conn.QueryAsync<LayoutVersion>(
            "SELECT * FROM kanban_layout_versions WHERE FactoryMapId=@MapId ORDER BY VersionNo DESC",
            new { MapId = mapId });
    }

    public async Task<LayoutVersion?> GetPublishedVersionAsync(int mapId)
    {
        // LA-4: same guard as GetVersionHistoryAsync — zero mapId returns null silently.
        if (mapId <= 0)
            throw new ArgumentOutOfRangeException(nameof(mapId),
                $"mapId must be positive, got {mapId}.");

        await using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<LayoutVersion>(
            "SELECT * FROM kanban_layout_versions WHERE FactoryMapId=@MapId AND Status=@Status",
            new { MapId = mapId, Status = (byte)LayoutStatus.Published });
    }

    public async Task RollbackAsync(int versionId)
    {
        // B-6 (input guard): Reject obviously invalid IDs early.
        if (versionId <= 0)
            throw new ArgumentOutOfRangeException(nameof(versionId),
                $"versionId must be a positive integer, got {versionId}.");

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        // B-6 fix: same transaction pattern as PublishAsync — archive + re-publish must be atomic.
        await using var tx = await conn.BeginTransactionAsync(IsolationLevel.RepeatableRead);

        var target = await conn.QueryFirstOrDefaultAsync<LayoutVersion>(
            "SELECT * FROM kanban_layout_versions WHERE Id=@Id FOR UPDATE",
            new { Id = versionId }, transaction: tx)
            ?? throw new InvalidOperationException($"Layout version {versionId} not found.");

        // B-7 fix: only Archived versions are valid rollback targets.
        // Rolling back a Draft would bypass the review/publish workflow.
        // Rolling back an already-Published version is a no-op that also corrupts PublishedAt.
        if (target.Status != LayoutStatus.Archived)
            throw new InvalidOperationException(
                $"Only Archived versions can be rolled back. Version {versionId} is currently '{target.Status}'.");

        // Archive the current Published version for this map.
        await conn.ExecuteAsync(
            """
            UPDATE kanban_layout_versions
            SET Status=@Archived
            WHERE FactoryMapId=@MapId AND Status=@Published
            """,
            new { Archived = (byte)LayoutStatus.Archived, MapId = target.FactoryMapId, Published = (byte)LayoutStatus.Published },
            transaction: tx);

        // B-8 fix: preserve the original PublishedAt so the audit trail shows when this version
        // was first published, not when the rollback happened. Use a separate RolledBackAt column
        // if rollback time also needs recording (schema change — not done here).
        await conn.ExecuteAsync(
            "UPDATE kanban_layout_versions SET Status=@Published WHERE Id=@Id",
            new { Published = (byte)LayoutStatus.Published, Id = versionId },
            transaction: tx);

        await tx.CommitAsync();
    }
}
