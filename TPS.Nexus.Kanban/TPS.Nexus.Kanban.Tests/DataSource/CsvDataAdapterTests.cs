using TPS.Nexus.Kanban.Core.Models;
using TPS.Nexus.Kanban.Services.DataSource;
using Xunit;

namespace TPS.Nexus.Kanban.Tests.DataSource;

public class CsvDataAdapterTests
{
    [Fact]
    public async Task FetchAsync_ParsesFirstDataRowAsFields()
    {
        var csvContent = "speed,temp,yield\n1200,68.3,98.2\n1100,70.1,97.5\n";
        var tmpFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tmpFile, csvContent);

        var config = new DataSourceConfig { FilePath = tmpFile };
        var adapter = new CsvDataAdapter();
        var result = await adapter.FetchAsync(config);

        Assert.Equal("1200", result.Fields["speed"]?.ToString());
        Assert.Equal("68.3", result.Fields["temp"]?.ToString());

        File.Delete(tmpFile);
    }
}
