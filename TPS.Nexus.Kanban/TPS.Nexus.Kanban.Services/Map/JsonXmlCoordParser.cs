using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.Map;

public class JsonXmlCoordParser
{
    public Task<FactoryMap> ParseJsonAsync(Stream file, string fileName)
    {
        var tmpPath = SaveToTemp(file, ".json");
        return Task.FromResult(new FactoryMap
        {
            FilePath = tmpPath,
            FormatType = MapFormatType.JsonCoord,
            CreatedAt = DateTime.UtcNow
        });
    }

    public Task<FactoryMap> ParseXmlAsync(Stream file, string fileName)
    {
        var tmpPath = SaveToTemp(file, ".xml");
        return Task.FromResult(new FactoryMap
        {
            FilePath = tmpPath,
            FormatType = MapFormatType.XmlCoord,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static string SaveToTemp(Stream file, string ext)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{ext}");
        using var fs = File.Create(path);
        file.CopyTo(fs);
        return path;
    }
}
