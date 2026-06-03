using System.Xml;
using System.Xml.XPath;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.DataSource;

public class XmlDataAdapter
{
    public Task<DataResult> FetchAsync(DataSourceConfig config)
    {
        var result = new DataResult();
        var path = config.FilePath
            ?? throw new InvalidOperationException(
                $"DataSourceConfig '{config.Name}' (Id={config.Id}): FilePath is required for XML source.");

        var doc = new XmlDocument();
        doc.Load(path);

        var xpath = string.IsNullOrEmpty(config.QueryOrPath) ? "/*" : config.QueryOrPath;

        // DS-7: wrap XPathException with config context so callers know which setting is broken
        XmlNode? node;
        try
        {
            node = doc.SelectSingleNode(xpath);
        }
        catch (XPathException ex)
        {
            throw new InvalidOperationException(
                $"DataSourceConfig '{config.Name}' (Id={config.Id}): QueryOrPath '{xpath}' is not valid XPath. " +
                $"Inner: {ex.Message}", ex);
        }

        if (node is XmlElement el)
        {
            foreach (XmlAttribute attr in el.Attributes)
                result.Fields[attr.Name] = attr.Value;
            foreach (XmlElement child in el.ChildNodes.OfType<XmlElement>())
                result.Fields[child.LocalName] = child.InnerText;
        }

        return Task.FromResult(result);
    }

    /// <remarks>
    /// DS-3: XML files are static snapshots without per-node timestamps. This method returns
    /// all matching nodes regardless of <paramref name="from"/> and <paramref name="to"/>. If
    /// time-range filtering is required, include a timestamp attribute and use an XPath
    /// predicate in <see cref="DataSourceConfig.QueryOrPath"/>.
    /// </remarks>
    public Task<IEnumerable<DataResult>> FetchHistoryAsync(DataSourceConfig config, DateTime from, DateTime to)
    {
        var results = new List<DataResult>();
        var path = config.FilePath
            ?? throw new InvalidOperationException(
                $"DataSourceConfig '{config.Name}' (Id={config.Id}): FilePath is required for XML source.");

        var doc = new XmlDocument();
        doc.Load(path);

        var xpath = string.IsNullOrEmpty(config.QueryOrPath) ? "//*" : config.QueryOrPath;

        XmlNodeList? nodes;
        try
        {
            nodes = doc.SelectNodes(xpath);
        }
        catch (XPathException ex)
        {
            throw new InvalidOperationException(
                $"DataSourceConfig '{config.Name}' (Id={config.Id}): QueryOrPath '{xpath}' is not valid XPath. " +
                $"Inner: {ex.Message}", ex);
        }

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
