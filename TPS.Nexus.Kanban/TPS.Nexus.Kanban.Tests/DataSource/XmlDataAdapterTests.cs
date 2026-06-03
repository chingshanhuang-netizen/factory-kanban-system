using TPS.Nexus.Kanban.Core.Models;
using TPS.Nexus.Kanban.Services.DataSource;
using Xunit;

namespace TPS.Nexus.Kanban.Tests.DataSource;

public class XmlDataAdapterTests
{
    [Fact]
    public async Task FetchAsync_ParsesXmlAttributesAsFields()
    {
        var xml = """
            <?xml version="1.0"?>
            <machine speed="1200" temp="68.3" status="running" />
            """;
        var tmpFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tmpFile, xml);

        var config = new DataSourceConfig { FilePath = tmpFile, QueryOrPath = "/machine" };
        var adapter = new XmlDataAdapter();
        var result = await adapter.FetchAsync(config);

        Assert.Equal("1200", result.Fields["speed"]?.ToString());
        Assert.Equal("running", result.Fields["status"]?.ToString());

        File.Delete(tmpFile);
    }
}
