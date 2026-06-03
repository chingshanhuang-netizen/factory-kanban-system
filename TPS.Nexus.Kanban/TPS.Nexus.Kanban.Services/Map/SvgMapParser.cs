using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.Map;

public class SvgMapParser
{
    private readonly string _storageRoot;

    public SvgMapParser(string storageRoot) => _storageRoot = storageRoot;

    public async Task<FactoryMap> ParseAsync(Stream file, string fileName)
    {
        var dir = Path.Combine(_storageRoot, "maps");
        Directory.CreateDirectory(dir);
        var savedName = $"{Guid.NewGuid()}.svg";
        var fullPath = Path.Combine(dir, savedName);

        await using var fs = File.Create(fullPath);
        await file.CopyToAsync(fs);

        return new FactoryMap
        {
            FilePath = $"/module-assets/TPS.Nexus.Kanban/maps/{savedName}",
            FormatType = Core.Enums.MapFormatType.Svg,
            CreatedAt = DateTime.UtcNow
        };
    }
}
