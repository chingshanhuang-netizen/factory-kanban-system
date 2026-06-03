using System.Diagnostics.CodeAnalysis;
using NSubstitute;
using System.Data;
using System.Data.Common;
using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Core.Models;
using TPS.Nexus.Kanban.Services.DataSource;
using Xunit;

namespace TPS.Nexus.Kanban.Tests.DataSource;

public class SqlDataAdapterTests
{
    // Mock abstract DbConnection/DbCommand/DbDataReader so that async methods
    // (OpenAsync, ExecuteReaderAsync, ReadAsync) can be properly stubbed.
    private readonly IDbConnectionFactory  _factory = Substitute.For<IDbConnectionFactory>();
    private readonly DbConnection          _conn    = Substitute.For<DbConnection>();
    private readonly DbCommand             _cmd     = Substitute.For<DbCommand>();
    private readonly DbDataReader          _reader  = Substitute.For<DbDataReader>();
    private readonly DbParameterCollection _params  = Substitute.For<DbParameterCollection>();

    public SqlDataAdapterTests()
    {
        _factory.CreateConnection().Returns(_conn);
        _conn.CreateCommand().Returns(_cmd);
        _cmd.Parameters.Returns(_params);

        // CreateParameter must return a concrete instance; NSubstitute returns null by default
        // for unstubbed methods, which causes NullReferenceException when setting ParameterName.
        _cmd.CreateParameter().Returns(_ => new FakeDbParameter());

        _conn.OpenAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task FetchAsync_ExecutesQueryAndReturnsFields()
    {
        var config = new DataSourceConfig
        {
            SourceType  = TPS.Nexus.Kanban.Core.Enums.DataSourceType.Sql,
            QueryOrPath = "SELECT speed, temp FROM machine WHERE id = @id",
            Parameters  = "{\"id\": 1}"
        };

        var callCount = 0;
        _reader.ReadAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(callCount++ == 0));
        _reader.FieldCount.Returns(2);
        _reader.GetName(0).Returns("speed");
        _reader.GetName(1).Returns("temp");
        _reader.GetValue(0).Returns(1200);
        _reader.GetValue(1).Returns(68.3);
        _cmd.ExecuteReaderAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DbDataReader>(_reader));

        var adapter = new SqlDataAdapter(_factory);
        var result  = await adapter.FetchAsync(config);

        Assert.NotNull(result);
        Assert.Equal(1200, result.Fields["speed"]);
        Assert.Equal(68.3, result.Fields["temp"]);
    }

    [Fact]
    public async Task FetchAsync_EmptyResult_ReturnsEmptyFields()
    {
        var config = new DataSourceConfig
        {
            SourceType  = TPS.Nexus.Kanban.Core.Enums.DataSourceType.Sql,
            QueryOrPath = "SELECT 1",
            Parameters  = null
        };

        _reader.ReadAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));
        _cmd.ExecuteReaderAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DbDataReader>(_reader));

        var adapter = new SqlDataAdapter(_factory);
        var result  = await adapter.FetchAsync(config);

        Assert.Empty(result.Fields);
    }

    // Minimal concrete DbParameter implementation required because DbParameter is abstract
    // and NSubstitute returns null for unstubbed methods on abstract types.
    private sealed class FakeDbParameter : DbParameter
    {
        public override DbType             DbType              { get; set; }
        public override ParameterDirection Direction           { get; set; }
        public override bool               IsNullable          { get; set; }
        [AllowNull] public override string  ParameterName       { get; set; } = string.Empty;
        public override int                Size                { get; set; }
        [AllowNull] public override string  SourceColumn        { get; set; } = string.Empty;
        public override bool               SourceColumnNullMapping { get; set; }
        public override DataRowVersion     SourceVersion       { get; set; }
        public override object?            Value               { get; set; }
        public override void               ResetDbType()       { }
    }
}
