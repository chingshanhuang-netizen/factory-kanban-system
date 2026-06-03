using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Interfaces;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.DataSource;

public class DataSourceService : IDataSourceService
{
    private readonly SqlDataAdapter _sql;
    private readonly CsvDataAdapter _csv;
    private readonly JsonDataAdapter _json;
    private readonly XmlDataAdapter _xml;

    public DataSourceService(IDbConnectionFactory dbFactory)
    {
        _sql = new SqlDataAdapter(dbFactory);
        _csv = new CsvDataAdapter();
        _json = new JsonDataAdapter();
        _xml = new XmlDataAdapter();
    }

    public Task<DataResult> FetchAsync(DataSourceConfig config)
    {
        // DS-1: guard null config before the switch accesses config.SourceType
        ArgumentNullException.ThrowIfNull(config);

        return config.SourceType switch
        {
            DataSourceType.Sql  => _sql.FetchAsync(config),
            DataSourceType.Csv  => _csv.FetchAsync(config),
            DataSourceType.Json => _json.FetchAsync(config),
            DataSourceType.Xml  => _xml.FetchAsync(config),
            _ => throw new NotSupportedException($"DataSourceType '{config.SourceType}' is not supported.")
        };
    }

    public Task<IEnumerable<DataResult>> FetchHistoryAsync(DataSourceConfig config, DateTime from, DateTime to)
    {
        // DS-1: guard null config
        ArgumentNullException.ThrowIfNull(config);

        // DS-2: reject inverted time ranges — callers passing from > to is always a bug
        if (from > to)
            throw new ArgumentException(
                $"'from' ({from:O}) must not be later than 'to' ({to:O}).", nameof(from));

        return config.SourceType switch
        {
            DataSourceType.Sql  => _sql.FetchHistoryAsync(config, from, to),
            DataSourceType.Csv  => _csv.FetchHistoryAsync(config, from, to),
            DataSourceType.Json => _json.FetchHistoryAsync(config, from, to),
            DataSourceType.Xml  => _xml.FetchHistoryAsync(config, from, to),
            _ => throw new NotSupportedException($"DataSourceType '{config.SourceType}' is not supported.")
        };
    }
}
