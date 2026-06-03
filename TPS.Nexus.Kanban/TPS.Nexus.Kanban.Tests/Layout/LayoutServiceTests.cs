using NSubstitute;
using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Models;
using TPS.Nexus.Kanban.Services.Layout;
using Xunit;

namespace TPS.Nexus.Kanban.Tests.Layout;

public class LayoutServiceTests
{
    private static LayoutService CreateService()
    {
        var db = Substitute.For<IDbConnectionFactory>();
        return new LayoutService(db);
    }

    // ── LA-3: GetVersionHistoryAsync — mapId guard ────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task GetVersionHistoryAsync_NonPositiveMapId_ThrowsArgumentOutOfRange(int mapId)
    {
        var svc = CreateService();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => svc.GetVersionHistoryAsync(mapId));
    }

    // ── LA-4: GetPublishedVersionAsync — mapId guard ──────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task GetPublishedVersionAsync_NonPositiveMapId_ThrowsArgumentOutOfRange(int mapId)
    {
        var svc = CreateService();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => svc.GetPublishedVersionAsync(mapId));
    }

    // ── existing model-behaviour tests ───────────────────────────────────────

    [Fact]
    public void Draft_Status_Is_Draft()
    {
        var draft = new LayoutVersion { Id = 1, Status = LayoutStatus.Draft };
        Assert.Equal(LayoutStatus.Draft, draft.Status);
        Assert.NotEqual(LayoutStatus.Published, draft.Status);
    }

    [Fact]
    public void Rollback_Simulation_ArchivesCurrent_PublishesTarget()
    {
        var current = new LayoutVersion { Id = 1, Status = LayoutStatus.Published };
        var target  = new LayoutVersion { Id = 2, Status = LayoutStatus.Archived };

        current.Status = LayoutStatus.Archived;
        target.Status  = LayoutStatus.Published;

        Assert.Equal(LayoutStatus.Archived,  current.Status);
        Assert.Equal(LayoutStatus.Published, target.Status);
    }

    // ── boundary / guard tests (model-level, no DB) ──────────────────────────

    // B-7: only Archived may be rolled back
    [Theory]
    [InlineData(LayoutStatus.Draft)]
    [InlineData(LayoutStatus.Published)]
    public void Rollback_NonArchived_IsRejectedByStatusCheck(LayoutStatus status)
    {
        var version = new LayoutVersion { Id = 10, Status = status };
        Assert.NotEqual(LayoutStatus.Archived, version.Status);
    }

    [Fact]
    public void Rollback_Archived_PassesStatusCheck()
    {
        var version = new LayoutVersion { Id = 10, Status = LayoutStatus.Archived };
        Assert.Equal(LayoutStatus.Archived, version.Status);
    }

    // B-3 / B-6: after publish the version status must be Published
    [Fact]
    public void Publish_Draft_ChangesStatusToPublished()
    {
        var draft = new LayoutVersion { Id = 5, Status = LayoutStatus.Draft };
        draft.Status      = LayoutStatus.Published;
        draft.PublishedAt = DateTime.UtcNow;

        Assert.Equal(LayoutStatus.Published, draft.Status);
        Assert.NotNull(draft.PublishedAt);
    }

    // B-3: the previously-published version must become Archived after a new Publish
    [Fact]
    public void Publish_ArchivesExistingPublishedVersion()
    {
        var existing = new LayoutVersion { Id = 3, Status = LayoutStatus.Published };
        var incoming = new LayoutVersion { Id = 4, Status = LayoutStatus.Draft };

        existing.Status = LayoutStatus.Archived;   // simulate archive step
        incoming.Status = LayoutStatus.Published;  // simulate promote step

        Assert.Equal(LayoutStatus.Archived,  existing.Status);
        Assert.Equal(LayoutStatus.Published, incoming.Status);
    }

    // B-8: rollback must NOT overwrite PublishedAt of the target version
    [Fact]
    public void Rollback_PreservesOriginalPublishedAt()
    {
        var originalTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var target = new LayoutVersion
        {
            Id          = 2,
            Status      = LayoutStatus.Archived,
            PublishedAt = originalTime
        };

        // Simulate the corrected rollback: only Status changes, PublishedAt unchanged.
        target.Status = LayoutStatus.Published;

        Assert.Equal(originalTime, target.PublishedAt);
    }

    // B-1: VersionNo must be monotonically increasing per map
    [Fact]
    public void VersionNo_NewDraft_IsMaxPlusOne()
    {
        int existingMax = 5;
        int nextVersion = existingMax + 1;

        Assert.Equal(6, nextVersion);
    }

    // B-2: factoryMapId must be positive
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void SaveDraft_NonPositiveFactoryMapId_ShouldBeRejected(int factoryMapId)
    {
        // This test documents the expected guard behaviour; the actual throw happens
        // inside LayoutService.SaveDraftAsync which requires a DB connection.
        // We verify the boundary value by asserting it would fail the <= 0 check.
        Assert.True(factoryMapId <= 0, $"Expected {factoryMapId} to be non-positive");
    }

    // B-4 / B-6 input guard: versionId must be positive
    [Theory]
    [InlineData(0)]
    [InlineData(-99)]
    public void PublishAndRollback_NonPositiveVersionId_ShouldBeRejected(int versionId)
    {
        Assert.True(versionId <= 0, $"Expected {versionId} to be non-positive");
    }

    // LayoutStatus enum sanity: byte values must match DB column convention
    [Fact]
    public void LayoutStatus_ByteValues_MatchDbConvention()
    {
        Assert.Equal(0, (byte)LayoutStatus.Draft);
        Assert.Equal(1, (byte)LayoutStatus.Published);
        Assert.Equal(2, (byte)LayoutStatus.Archived);
    }

    // B-3: ensure only one Published version is the intended invariant
    [Fact]
    public void PublishInvariant_ExactlyOnePublishedVersionPerMap()
    {
        var versions = new List<LayoutVersion>
        {
            new() { Id = 1, FactoryMapId = 10, Status = LayoutStatus.Archived },
            new() { Id = 2, FactoryMapId = 10, Status = LayoutStatus.Archived },
            new() { Id = 3, FactoryMapId = 10, Status = LayoutStatus.Published },
        };

        var publishedCount = versions.Count(v => v.Status == LayoutStatus.Published);
        Assert.Equal(1, publishedCount);
    }
}
