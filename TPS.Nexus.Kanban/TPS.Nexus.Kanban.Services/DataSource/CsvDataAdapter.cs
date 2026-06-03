using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.DataSource;

public class CsvDataAdapter
{
    public Task<DataResult> FetchAsync(DataSourceConfig config)
    {
        var result = new DataResult();
        var path = config.FilePath
            ?? throw new InvalidOperationException("FilePath is required for CSV source.");

        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null
        };

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, csvConfig);

        csv.Read();
        csv.ReadHeader();
        if (csv.Read())
        {
            foreach (var header in csv.HeaderRecord ?? Array.Empty<string>())
                result.Fields[header] = csv.GetField(header);
        }

        return Task.FromResult(result);
    }

    public Task<IEnumerable<DataResult>> FetchHistoryAsync(DataSourceConfig config, DateTime from, DateTime to)
    {
        var results = new List<DataResult>();
        var path = config.FilePath
            ?? throw new InvalidOperationException("FilePath is required for CSV source.");

        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null
        };

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, csvConfig);

        csv.Read();
        csv.ReadHeader();
        while (csv.Read())
        {
            var row = new DataResult();
            foreach (var header in csv.HeaderRecord ?? Array.Empty<string>())
                row.Fields[header] = csv.GetField(header);
            results.Add(row);
        }

        return Task.FromResult<IEnumerable<DataResult>>(results);
    }
}
