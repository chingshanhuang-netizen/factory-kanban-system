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
            ?? throw new InvalidOperationException(
                $"DataSourceConfig '{config.Name}' (Id={config.Id}): FilePath is required for CSV source.");

        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null
        };

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, csvConfig);

        // DS-6: read header only if the file has at least one row; an empty file must not
        // throw CsvHelperException from ReadHeader() being called before Read() returns true.
        if (!csv.Read()) return Task.FromResult(result);
        csv.ReadHeader();

        if (csv.Read())
        {
            foreach (var header in csv.HeaderRecord ?? Array.Empty<string>())
                result.Fields[header] = csv.GetField(header);
        }

        return Task.FromResult(result);
    }

    /// <remarks>
    /// DS-3: CSV files are static snapshots without per-row timestamps. This method returns
    /// all rows regardless of <paramref name="from"/> and <paramref name="to"/>. If time-range
    /// filtering is required, add a timestamp column to the CSV and filter by it in the query.
    /// </remarks>
    public Task<IEnumerable<DataResult>> FetchHistoryAsync(DataSourceConfig config, DateTime from, DateTime to)
    {
        var results = new List<DataResult>();
        var path = config.FilePath
            ?? throw new InvalidOperationException(
                $"DataSourceConfig '{config.Name}' (Id={config.Id}): FilePath is required for CSV source.");

        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null
        };

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, csvConfig);

        // DS-6: guard empty file before ReadHeader()
        if (!csv.Read()) return Task.FromResult<IEnumerable<DataResult>>(results);
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
