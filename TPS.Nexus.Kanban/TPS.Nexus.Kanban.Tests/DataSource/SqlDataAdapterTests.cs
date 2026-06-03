using NSubstitute;
using System.Data;
using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Core.Models;
using TPS.Nexus.Kanban.Services.DataSource;
using Xunit;

namespace TPS.Nexus.Kanban.Tests.DataSource;

public class SqlDataAdapterTests
{
    private readonly IDbConnectionFactory _factory = Substitute.For<IDbConnectionFactory>();
    private readonly IDbConnection _conn = Substitute.For<IDbConnection>();
    private readonly IDbCommand _cmd = Substitute.For<IDbCommand>();
    private readonly IDataParameterCollection _params = Substitute.For<IDataParameterCollection>();
    private readonly IDataReader _reader = Substitute.For<IDataReader>();

    public SqlDataAdapterTests()
    {
        _factory.CreateConnection().Returns(_conn);
        _conn.CreateCommand().Returns(_cmd);
        _cmd.Parameters.Returns(_params);
    }

    [Fact]
    public async Task FetchAsync_ExecutesQueryAndReturnsFields()
    {
        var config = new DataSourceConfig
        {
            SourceType = TPS.Nexus.Kanban.Core.Enums.DataSourceType.Sql,
            QueryOrPath = "SELECT speed, temp FROM machine WHERE id = @id",
            Parameters = "{\"id\": 1}"
        };

        var callCount = 0;
        _reader.Read().Returns(_ => callCount++ == 0);
        _reader.FieldCount.Returns(2);
        _reader.GetName(0).Returns("speed");
        _reader.GetName(1).Returns("temp");
        _reader.GetValue(0).Returns(1200);
        _reader.GetValue(1).Returns(68.3);
        _cmd.ExecuteReader().Returns(_reader);

        var adapter = new SqlDataAdapter(_factory);
        var result = await adapter.FetchAsync(config);

        Assert.NotNull(result);
        Assert.Equal(1200, result.Fields["speed"]);
        Assert.Equal(68.3, result.Fields["temp"]);
    }

    [Fact]
    public async Task FetchAsync_EmptyResult_ReturnsEmptyFields()
    {
        var config = new DataSourceConfig
        {
            SourceType = TPS.Nexus.Kanban.Core.Enums.DataSourceType.Sql,
            QueryOrPath = "SELECT 1",
            Parameters = null
        };

        _reader.Read().Returns(false);
        _cmd.ExecuteReader().Returns(_reader);

        var adapter = new SqlDataAdapter(_factory);
        var result = await adapter.FetchAsync(config);

        Assert.Empty(result.Fields);
    }
}
