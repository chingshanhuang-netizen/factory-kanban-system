using MySqlConnector;
using System.Data.Common;
using TPS.Nexus.Core;

namespace TPS.Nexus.Kanban.Demo.Infrastructure;

public class MySqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public MySqlConnectionFactory(string connectionString) =>
        _connectionString = connectionString;

    public DbConnection CreateConnection() =>
        new MySqlConnection(_connectionString);
}
