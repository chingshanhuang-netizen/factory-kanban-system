using TPS.Nexus.Kanban.Core.Interfaces;
using TPS.Nexus.Kanban.Core.Models;
namespace TPS.Nexus.Kanban.Services.DataSource;
public class DataSourceService : IDataSourceService
{
    public Task<DataResult> FetchAsync(DataSourceConfig config) => throw new NotImplementedException();
    public Task<IEnumerable<DataResult>> FetchHistoryAsync(DataSourceConfig config, DateTime from, DateTime to) => throw new NotImplementedException();
}
