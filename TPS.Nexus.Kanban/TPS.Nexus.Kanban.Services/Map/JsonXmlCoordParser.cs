using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.Map;

public class JsonXmlCoordParser
{
    public async Task<FactoryMap> ParseJsonAsync(Stream file, string fileName)
    {
        var tmpPath = await SaveToTempAsync(file, ".json");
        return new FactoryMap
        {
            FilePath = tmpPath,
            FormatType = MapFormatType.JsonCoord,
            CreatedAt = DateTime.UtcNow
        };
    }

    public async Task<FactoryMap> ParseXmlAsync(Stream file, string fileName)
    {
        var tmpPath = await SaveToTempAsync(file, ".xml");
        return new FactoryMap
        {
            FilePath = tmpPath,
            FormatType = MapFormatType.XmlCoord,
            CreatedAt = DateTime.UtcNow
        };
    }

    // async copy avoids blocking the thread pool when the caller's stream is a large upload.
    private static async Task<string> SaveToTempAsync(Stream file, string ext)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{ext}");
        await using var fs = File.Create(path);
        await file.CopyToAsync(fs);
        return path;
    }
}
