using TPS.Nexus.Kanban.Core.Models;
using TPS.Nexus.Kanban.Services.DataSource;
using Xunit;

namespace TPS.Nexus.Kanban.Tests.DataSource;

public class JsonDataAdapterTests
{
    [Fact]
    public async Task FetchAsync_ParsesFlatJsonAsFields()
    {
        var json = """{"speed":1200,"temp":68.3,"status":"running"}""";
        var tmpFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tmpFile, json);

        var config = new DataSourceConfig { FilePath = tmpFile };
        var adapter = new JsonDataAdapter();
        var result = await adapter.FetchAsync(config);

        Assert.Equal(1200, Convert.ToInt32(result.Fields["speed"]));
        Assert.Equal("running", result.Fields["status"]?.ToString());

        File.Delete(tmpFile);
    }
}
