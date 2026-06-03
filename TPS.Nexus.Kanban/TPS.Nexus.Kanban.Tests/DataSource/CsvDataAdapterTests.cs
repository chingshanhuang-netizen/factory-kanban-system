using TPS.Nexus.Kanban.Core.Models;
using TPS.Nexus.Kanban.Services.DataSource;
using Xunit;

namespace TPS.Nexus.Kanban.Tests.DataSource;

public class CsvDataAdapterTests
{
    // ── existing happy-path test ──────────────────────────────────────────────

    [Fact]
    public async Task FetchAsync_ParsesFirstDataRowAsFields()
    {
        var tmpFile = await WriteTempFileAsync("speed,temp,yield\n1200,68.3,98.2\n1100,70.1,97.5\n");
        var config  = new DataSourceConfig { FilePath = tmpFile };

        var result = await new CsvDataAdapter().FetchAsync(config);

        Assert.Equal("1200", result.Fields["speed"]?.ToString());
        Assert.Equal("68.3", result.Fields["temp"]?.ToString());
        File.Delete(tmpFile);
    }

    // ── DS-6: empty file must not throw ──────────────────────────────────────

    [Fact]
    public async Task FetchAsync_EmptyFile_ReturnsEmptyFields()
    {
        var tmpFile = await WriteTempFileAsync("");
        var config  = new DataSourceConfig { Name = "test", FilePath = tmpFile };

        var result = await new CsvDataAdapter().FetchAsync(config);

        Assert.Empty(result.Fields);
        File.Delete(tmpFile);
    }

    [Fact]
    public async Task FetchHistoryAsync_EmptyFile_ReturnsEmptyList()
    {
        var tmpFile = await WriteTempFileAsync("");
        var config  = new DataSourceConfig { Name = "test", FilePath = tmpFile };

        var results = await new CsvDataAdapter().FetchHistoryAsync(config, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);

        Assert.Empty(results);
        File.Delete(tmpFile);
    }

    // ── DS-6: header-only file (no data rows) ────────────────────────────────

    [Fact]
    public async Task FetchAsync_HeaderOnlyFile_ReturnsEmptyFields()
    {
        var tmpFile = await WriteTempFileAsync("speed,temp,yield\n");
        var config  = new DataSourceConfig { Name = "test", FilePath = tmpFile };

        var result = await new CsvDataAdapter().FetchAsync(config);

        Assert.Empty(result.Fields);
        File.Delete(tmpFile);
    }

    // ── DS-1: null FilePath throws with clear message ─────────────────────────

    [Fact]
    public async Task FetchAsync_NullFilePath_ThrowsInvalidOperation()
    {
        var config = new DataSourceConfig { Name = "test", FilePath = null };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new CsvDataAdapter().FetchAsync(config));

        Assert.Contains("FilePath", ex.Message);
        Assert.Contains("test", ex.Message);
    }

    // ── DS-3: FetchHistoryAsync returns all rows (time params are no-ops for CSV) ──

    [Fact]
    public async Task FetchHistoryAsync_ReturnsAllRows_RegardlessOfTimeRange()
    {
        var tmpFile = await WriteTempFileAsync("val\n10\n20\n30\n");
        var config  = new DataSourceConfig { Name = "test", FilePath = tmpFile };

        // Narrow time window — CSV adapter cannot filter by time, returns all 3 rows
        var results = (await new CsvDataAdapter()
            .FetchHistoryAsync(config, DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow)).ToList();

        Assert.Equal(3, results.Count);
        File.Delete(tmpFile);
    }

    private static async Task<string> WriteTempFileAsync(string content)
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, content);
        return path;
    }
}
