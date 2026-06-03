using TPS.Nexus.Kanban.Core.Constants;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.Map;

/// <summary>
/// DXF→SVG conversion. Full implementation requires netDxf library.
/// Install netDxf from https://github.com/haplokuon/netDXF and add as local reference.
/// Currently returns a placeholder SVG.
/// </summary>
public class DxfMapParser
{
    private readonly string _storageRoot;

    public DxfMapParser(string storageRoot) => _storageRoot = storageRoot;

    public async Task<FactoryMap> ParseAsync(Stream file, string fileName)
    {
        var dir = Path.Combine(_storageRoot, KanbanAssets.MapsSubdir);
        Directory.CreateDirectory(dir);
        var savedName = $"{Guid.NewGuid()}.svg";
        var fullPath = Path.Combine(dir, savedName);

        // Placeholder SVG until netDxf is available
        var placeholder = """<svg xmlns="http://www.w3.org/2000/svg" width="1000" height="800"><text x="10" y="20" fill="#4a6a8a">DXF map (conversion pending netDxf library)</text></svg>""";
        await File.WriteAllTextAsync(fullPath, placeholder);

        return new FactoryMap
        {
            FilePath = $"{KanbanAssets.ModulePrefix}/{KanbanAssets.MapsSubdir}/{savedName}",
            FormatType = Core.Enums.MapFormatType.Dxf,
            CreatedAt = DateTime.UtcNow
        };
    }
}
