using TPS.Nexus.Kanban.Core.Interfaces;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Demo.Mocks;

public class DemoDataSourceService : IDataSourceService
{
    private static readonly Random _rng = new(42);

    public Task<DataResult> FetchAsync(DataSourceConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var r = new DataResult();
        r.Fields["value"]     = Math.Round(60 + _rng.NextDouble() * 40, 1);
        r.Fields["unit"]      = "℃";
        r.Fields["timestamp"] = DateTime.UtcNow.ToString("HH:mm:ss");
        return Task.FromResult(r);
    }

    public Task<IEnumerable<DataResult>> FetchHistoryAsync(DataSourceConfig config, DateTime from, DateTime to)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (from > to) throw new ArgumentException("'from' 不可晚於 'to'。", nameof(from));

        var results = Enumerable.Range(0, 12).Select(i =>
        {
            var r = new DataResult();
            r.Fields["value"]     = Math.Round(55 + _rng.NextDouble() * 45, 1);
            r.Fields["timestamp"] = from.AddMinutes(i * 5).ToString("HH:mm");
            return r;
        });
        return Task.FromResult(results);
    }
}
