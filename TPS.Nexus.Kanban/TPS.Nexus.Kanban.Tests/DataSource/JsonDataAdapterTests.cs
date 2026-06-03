using TPS.Nexus.Kanban.Core.Models;
using TPS.Nexus.Kanban.Services.DataSource;
using Xunit;

namespace TPS.Nexus.Kanban.Tests.DataSource;

public class JsonDataAdapterTests
{
    // ── existing happy-path test ──────────────────────────────────────────────

    [Fact]
    public async Task FetchAsync_ParsesFlatJsonAsFields()
    {
        var tmpFile = await WriteTempFileAsync("""{"speed":1200,"temp":68.3,"status":"running"}""");
        var config  = new DataSourceConfig { FilePath = tmpFile };

        var result = await new JsonDataAdapter().FetchAsync(config);

        Assert.Equal(1200, Convert.ToInt32(result.Fields["speed"]));
        Assert.Equal("running", result.Fields["status"]?.ToString());
        File.Delete(tmpFile);
    }

    // ── DS-5: NavigatePath partial match must throw, not silently return wrong node ──

    [Fact]
    public async Task FetchAsync_MissingPathSegment_ThrowsKeyNotFound()
    {
        var json    = """{"sensors":{"pressure":5.2}}""";
        var tmpFile = await WriteTempFileAsync(json);
        var config  = new DataSourceConfig
        {
            Name         = "test",
            FilePath     = tmpFile,
            QueryOrPath  = "/sensors/temperature"   // 'temperature' does not exist
        };

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => new JsonDataAdapter().FetchAsync(config));

        Assert.Contains("temperature", ex.Message);
        Assert.Contains("test", ex.Message);
        File.Delete(tmpFile);
    }

    [Fact]
    public async Task FetchAsync_ValidNestedPath_ReturnsCorrectFields()
    {
        var json    = """{"data":{"machine":{"speed":1200,"temp":68}}}""";
        var tmpFile = await WriteTempFileAsync(json);
        var config  = new DataSourceConfig
        {
            FilePath    = tmpFile,
            QueryOrPath = "/data/machine"
        };

        var result = await new JsonDataAdapter().FetchAsync(config);

        Assert.True(result.Fields.ContainsKey("speed"));
        Assert.True(result.Fields.ContainsKey("temp"));
        File.Delete(tmpFile);
    }

    // ── DS-1: null FilePath throws with config context ────────────────────────

    [Fact]
    public async Task FetchAsync_NullFilePath_ThrowsInvalidOperation()
    {
        var config = new DataSourceConfig { Name = "mysource", FilePath = null };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new JsonDataAdapter().FetchAsync(config));

        Assert.Contains("FilePath", ex.Message);
        Assert.Contains("mysource", ex.Message);
    }

    // ── DS-3: FetchHistoryAsync — time params are no-ops for JSON files ───────

    [Fact]
    public async Task FetchHistoryAsync_ReturnsAllItems_RegardlessOfTimeRange()
    {
        var json    = """[{"val":1},{"val":2},{"val":3}]""";
        var tmpFile = await WriteTempFileAsync(json);
        var config  = new DataSourceConfig { Name = "test", FilePath = tmpFile };

        var results = (await new JsonDataAdapter()
            .FetchHistoryAsync(config, DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow)).ToList();

        Assert.Equal(3, results.Count);
        File.Delete(tmpFile);
    }

    // ── empty / non-object root ───────────────────────────────────────────────

    [Fact]
    public async Task FetchAsync_NonObjectRoot_ReturnsEmptyFields()
    {
        var tmpFile = await WriteTempFileAsync("[1,2,3]");
        var config  = new DataSourceConfig { Name = "test", FilePath = tmpFile };

        var result = await new JsonDataAdapter().FetchAsync(config);

        Assert.Empty(result.Fields);
        File.Delete(tmpFile);
    }

    // ── DA-2: FileNotFoundException wrapped with config context ──────────────

    [Fact]
    public async Task FetchAsync_MissingFile_ThrowsInvalidOperationWithContext()
    {
        var config = new DataSourceConfig
        {
            Name     = "sensor-json",
            Id       = 5,
            FilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json")
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new JsonDataAdapter().FetchAsync(config));

        Assert.Contains("sensor-json", ex.Message);
        Assert.Contains("5", ex.Message);
    }

    [Fact]
    public async Task FetchHistoryAsync_MissingFile_ThrowsInvalidOperationWithContext()
    {
        var config = new DataSourceConfig
        {
            Name     = "history-json",
            Id       = 9,
            FilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json")
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new JsonDataAdapter().FetchHistoryAsync(config, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow));

        Assert.Contains("history-json", ex.Message);
    }

    // ── DA-3: JsonException (malformed file) wrapped with config context ──────

    [Fact]
    public async Task FetchAsync_InvalidJson_ThrowsInvalidOperationWithContext()
    {
        var tmpFile = await WriteTempFileAsync("{ not valid json {{{{");
        var config  = new DataSourceConfig { Name = "bad-json", Id = 3, FilePath = tmpFile };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new JsonDataAdapter().FetchAsync(config));

        Assert.Contains("bad-json", ex.Message);
        Assert.Contains("invalid JSON", ex.Message);
        File.Delete(tmpFile);
    }

    [Fact]
    public async Task FetchHistoryAsync_InvalidJson_ThrowsInvalidOperationWithContext()
    {
        var tmpFile = await WriteTempFileAsync("<<<not json>>>");
        var config  = new DataSourceConfig { Name = "bad-json-hist", Id = 4, FilePath = tmpFile };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new JsonDataAdapter().FetchHistoryAsync(config, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow));

        Assert.Contains("bad-json-hist", ex.Message);
        Assert.Contains("invalid JSON", ex.Message);
        File.Delete(tmpFile);
    }

    private static async Task<string> WriteTempFileAsync(string content)
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, content);
        return path;
    }
}
