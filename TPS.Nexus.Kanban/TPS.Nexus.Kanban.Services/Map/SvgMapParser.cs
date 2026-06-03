using TPS.Nexus.Kanban.Core.Constants;
using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.Map;

public class SvgMapParser
{
    private readonly string _storageRoot;

    public SvgMapParser(string storageRoot) => _storageRoot = storageRoot;

    public async Task<FactoryMap> ParseAsync(Stream file, string fileName)
    {
        var dir = Path.Combine(_storageRoot, KanbanAssets.MapsSubdir);
        Directory.CreateDirectory(dir);
        var savedName = $"{Guid.NewGuid()}.svg";
        var fullPath = Path.Combine(dir, savedName);

        await using var fs = File.Create(fullPath);
        await file.CopyToAsync(fs);

        return new FactoryMap
        {
            FilePath = $"{KanbanAssets.ModulePrefix}/{KanbanAssets.MapsSubdir}/{savedName}",
            FormatType = MapFormatType.Svg,
            CreatedAt = DateTime.UtcNow
        };
    }
}
