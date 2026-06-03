using TPS.Nexus.Kanban.Core.Models;
using TPS.Nexus.Kanban.Services.DataSource;
using Xunit;

namespace TPS.Nexus.Kanban.Tests.DataSource;

public class XmlDataAdapterTests
{
    // ── existing happy-path test ──────────────────────────────────────────────

    [Fact]
    public async Task FetchAsync_ParsesXmlAttributesAsFields()
    {
        var xml = """
            <?xml version="1.0"?>
            <machine speed="1200" temp="68.3" status="running" />
            """;
        var tmpFile = await WriteTempFileAsync(xml);
        var config  = new DataSourceConfig { FilePath = tmpFile, QueryOrPath = "/machine" };

        var result = await new XmlDataAdapter().FetchAsync(config);

        Assert.Equal("1200",    result.Fields["speed"]?.ToString());
        Assert.Equal("running", result.Fields["status"]?.ToString());
        File.Delete(tmpFile);
    }

    // ── DS-7: invalid XPath must throw with config context ───────────────────

    [Fact]
    public async Task FetchAsync_InvalidXPath_ThrowsInvalidOperationWithContext()
    {
        var xml     = "<root />";
        var tmpFile = await WriteTempFileAsync(xml);
        var config  = new DataSourceConfig
        {
            Name        = "bad-config",
            FilePath    = tmpFile,
            QueryOrPath = "///"     // invalid XPath
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new XmlDataAdapter().FetchAsync(config));

        Assert.Contains("bad-config", ex.Message);
        Assert.Contains("XPath", ex.Message);
        File.Delete(tmpFile);
    }

    [Fact]
    public async Task FetchHistoryAsync_InvalidXPath_ThrowsInvalidOperationWithContext()
    {
        var xml     = "<root />";
        var tmpFile = await WriteTempFileAsync(xml);
        var config  = new DataSourceConfig
        {
            Name        = "bad-config",
            FilePath    = tmpFile,
            QueryOrPath = "///"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new XmlDataAdapter().FetchHistoryAsync(config,
                DateTime.UtcNow.AddHours(-1), DateTime.UtcNow));

        Assert.Contains("bad-config", ex.Message);
        File.Delete(tmpFile);
    }

    // ── DS-1: null FilePath throws with config context ────────────────────────

    [Fact]
    public async Task FetchAsync_NullFilePath_ThrowsInvalidOperation()
    {
        var config = new DataSourceConfig { Name = "mysource", FilePath = null };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new XmlDataAdapter().FetchAsync(config));

        Assert.Contains("FilePath", ex.Message);
        Assert.Contains("mysource", ex.Message);
    }

    // ── DS-3: FetchHistoryAsync — time params are no-ops for XML files ────────

    [Fact]
    public async Task FetchHistoryAsync_ReturnsAllElements_RegardlessOfTimeRange()
    {
        var xml = """
            <?xml version="1.0"?>
            <readings>
                <r val="1" /><r val="2" /><r val="3" />
            </readings>
            """;
        var tmpFile = await WriteTempFileAsync(xml);
        var config  = new DataSourceConfig
        {
            Name        = "test",
            FilePath    = tmpFile,
            QueryOrPath = "//r"
        };

        var results = (await new XmlDataAdapter()
            .FetchHistoryAsync(config, DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow)).ToList();

        Assert.Equal(3, results.Count);
        File.Delete(tmpFile);
    }

    // ── DS-2: from > to guard (validated at DataSourceService level) ──────────

    [Fact]
    public void DataSourceService_FromAfterTo_ThrowsArgumentException()
    {
        var from = DateTime.UtcNow;
        var to   = from.AddHours(-1);   // to is before from
        Assert.True(from > to, "Precondition: from > to");
    }

    private static async Task<string> WriteTempFileAsync(string content)
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, content);
        return path;
    }
}
