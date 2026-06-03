using NSubstitute;
using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Models;
using TPS.Nexus.Kanban.Services.DataSource;
using Xunit;

namespace TPS.Nexus.Kanban.Tests.DataSource;

public class DataSourceServiceTests
{
    private static DataSourceService CreateService()
    {
        var db = Substitute.For<IDbConnectionFactory>();
        return new DataSourceService(db);
    }

    // ── DS-1: null config guard ───────────────────────────────────────────────

    [Fact]
    public async Task FetchAsync_NullConfig_ThrowsArgumentNullException()
    {
        var svc = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => svc.FetchAsync(null!));
    }

    [Fact]
    public async Task FetchHistoryAsync_NullConfig_ThrowsArgumentNullException()
    {
        var svc = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => svc.FetchHistoryAsync(null!, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow));
    }

    // ── DS-2: inverted time range guard ──────────────────────────────────────

    [Fact]
    public async Task FetchHistoryAsync_FromAfterTo_ThrowsArgumentException()
    {
        var svc    = CreateService();
        var config = new DataSourceConfig { Name = "test", SourceType = DataSourceType.Csv, FilePath = "/tmp/x.csv" };
        var from   = DateTime.UtcNow;
        var to     = from.AddSeconds(-1);   // to is before from

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => svc.FetchHistoryAsync(config, from, to));

        Assert.Contains("from", ex.Message);
        Assert.Contains("to", ex.Message);
    }

    [Fact]
    public async Task FetchHistoryAsync_EqualFromTo_DoesNotThrow()
    {
        // Equal timestamps are not an inversion — a zero-width window is valid.
        var tmpFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tmpFile, "val\n1\n");
        try
        {
            var svc    = CreateService();
            var config = new DataSourceConfig { Name = "test", SourceType = DataSourceType.Csv, FilePath = tmpFile };
            var now    = DateTime.UtcNow;

            // Should not throw; CSV adapter ignores time range anyway
            var results = await svc.FetchHistoryAsync(config, now, now);
            Assert.NotNull(results);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    // ── Unsupported DataSourceType ────────────────────────────────────────────

    [Fact]
    public async Task FetchAsync_UnsupportedSourceType_ThrowsNotSupportedException()
    {
        var svc    = CreateService();
        var config = new DataSourceConfig { Name = "test", SourceType = (DataSourceType)99 };

        await Assert.ThrowsAsync<NotSupportedException>(
            () => svc.FetchAsync(config));
    }

    [Fact]
    public async Task FetchHistoryAsync_UnsupportedSourceType_ThrowsNotSupportedException()
    {
        var svc    = CreateService();
        var config = new DataSourceConfig { Name = "test", SourceType = (DataSourceType)99 };
        var now    = DateTime.UtcNow;

        await Assert.ThrowsAsync<NotSupportedException>(
            () => svc.FetchHistoryAsync(config, now.AddHours(-1), now));
    }
}
