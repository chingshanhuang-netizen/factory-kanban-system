using TPS.Nexus.Kanban.Core.Constants;
using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.Map;

public class ImageMapHandler
{
    private readonly string _storageRoot;

    public ImageMapHandler(string storageRoot) => _storageRoot = storageRoot;

    public async Task<FactoryMap> HandleAsync(Stream file, string fileName, MapFormatType format)
    {
        var dir = Path.Combine(_storageRoot, KanbanAssets.MapsSubdir);
        Directory.CreateDirectory(dir);
        var ext = format == MapFormatType.Png ? ".png" : ".jpg";
        var savedName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(dir, savedName);

        await using var fs = File.Create(fullPath);
        await file.CopyToAsync(fs);

        return new FactoryMap
        {
            FilePath = $"{KanbanAssets.ModulePrefix}/{KanbanAssets.MapsSubdir}/{savedName}",
            FormatType = format,
            CreatedAt = DateTime.UtcNow
        };
    }
}
