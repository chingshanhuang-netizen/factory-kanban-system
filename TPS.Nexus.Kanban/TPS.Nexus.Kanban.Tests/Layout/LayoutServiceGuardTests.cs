using NSubstitute;
using System.Data.Common;
using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Services.Layout;
using Xunit;

namespace TPS.Nexus.Kanban.Tests.Layout;

/// <summary>
/// Actual service-level guard tests for LayoutService — each call goes through the real
/// service instance so the guard throw is exercised, unlike the trivial Assert.True(x &lt;= 0)
/// assertions in LayoutServiceTests which only document the expected rule.
/// </summary>
public class LayoutServiceGuardTests
{
    private static LayoutService CreateService() =>
        new LayoutService(Substitute.For<IDbConnectionFactory>());

    // ── SaveDraftAsync: factoryMapId guard ────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task SaveDraftAsync_NonPositiveFactoryMapId_ThrowsArgumentOutOfRange(int factoryMapId)
    {
        var svc = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => svc.SaveDraftAsync(factoryMapId, "{}", "user1"));
    }

    // ── SaveDraftAsync: S-6 null/whitespace string guards ────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveDraftAsync_NullOrWhitespaceLayoutJson_ThrowsArgumentException(string? layoutJson)
    {
        var svc = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.SaveDraftAsync(1, layoutJson!, "user1"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveDraftAsync_NullOrWhitespaceCreatedBy_ThrowsArgumentException(string? createdBy)
    {
        var svc = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.SaveDraftAsync(1, "{}", createdBy!));
    }

    // ── PublishAsync: draftId guard ───────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task PublishAsync_NonPositiveDraftId_ThrowsArgumentOutOfRange(int draftId)
    {
        var svc = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => svc.PublishAsync(draftId));
    }

    // ── RollbackAsync: versionId guard ────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task RollbackAsync_NonPositiveVersionId_ThrowsArgumentOutOfRange(int versionId)
    {
        var svc = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => svc.RollbackAsync(versionId));
    }

    // ── DB connection timeout: verify the service propagates, not swallows ────

    [Fact]
    public async Task SaveDraftAsync_DbConnectionTimeout_PropagatesException()
    {
        var conn = Substitute.For<DbConnection>();
        conn.OpenAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new TimeoutException("DB connection timed out")));

        var db = Substitute.For<IDbConnectionFactory>();
        db.CreateConnection().Returns(conn);

        var svc = new LayoutService(db);

        // Guard passes (factoryMapId=1 is valid), then the first DB call (conn.OpenAsync) throws.
        await Assert.ThrowsAsync<TimeoutException>(
            () => svc.SaveDraftAsync(1, "{}", "user1"));
    }

    [Fact]
    public async Task PublishAsync_DbConnectionTimeout_PropagatesException()
    {
        var conn = Substitute.For<DbConnection>();
        conn.OpenAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new TimeoutException("DB connection timed out")));

        var db = Substitute.For<IDbConnectionFactory>();
        db.CreateConnection().Returns(conn);

        var svc = new LayoutService(db);

        await Assert.ThrowsAsync<TimeoutException>(
            () => svc.PublishAsync(1));
    }

    [Fact]
    public async Task RollbackAsync_DbConnectionTimeout_PropagatesException()
    {
        var conn = Substitute.For<DbConnection>();
        conn.OpenAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new TimeoutException("DB connection timed out")));

        var db = Substitute.For<IDbConnectionFactory>();
        db.CreateConnection().Returns(conn);

        var svc = new LayoutService(db);

        await Assert.ThrowsAsync<TimeoutException>(
            () => svc.RollbackAsync(1));
    }
}
