using System.Xml;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.DataSource;

public class XmlDataAdapter
{
    public Task<DataResult> FetchAsync(DataSourceConfig config)
    {
        var result = new DataResult();
        var path = config.FilePath
            ?? throw new InvalidOperationException("FilePath is required for XML source.");

        var doc = new XmlDocument();
        doc.Load(path);

        var xpath = string.IsNullOrEmpty(config.QueryOrPath) ? "/*" : config.QueryOrPath;
        var node = doc.SelectSingleNode(xpath);
        if (node is XmlElement el)
        {
            foreach (XmlAttribute attr in el.Attributes)
                result.Fields[attr.Name] = attr.Value;
            foreach (XmlElement child in el.ChildNodes.OfType<XmlElement>())
                result.Fields[child.LocalName] = child.InnerText;
        }

        return Task.FromResult(result);
    }

    public Task<IEnumerable<DataResult>> FetchHistoryAsync(DataSourceConfig config, DateTime from, DateTime to)
    {
        var results = new List<DataResult>();
        var path = config.FilePath
            ?? throw new InvalidOperationException("FilePath is required for XML source.");

        var doc = new XmlDocument();
        doc.Load(path);

        var xpath = string.IsNullOrEmpty(config.QueryOrPath) ? "//*" : config.QueryOrPath;
        var nodes = doc.SelectNodes(xpath);
        if (nodes == null) return Task.FromResult<IEnumerable<DataResult>>(results);

        foreach (XmlElement elem in nodes.OfType<XmlElement>())
        {
            var row = new DataResult();
            foreach (XmlAttribute attr in elem.Attributes)
                row.Fields[attr.Name] = attr.Value;
            results.Add(row);
        }

        return Task.FromResult<IEnumerable<DataResult>>(results);
    }
}
