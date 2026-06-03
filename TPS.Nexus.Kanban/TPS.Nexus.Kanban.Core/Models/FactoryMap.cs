using TPS.Nexus.Kanban.Core.Enums;

namespace TPS.Nexus.Kanban.Core.Models;

public class FactoryMap
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public MapFormatType FormatType { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string? ThumbnailPath { get; set; }
    public DateTime CreatedAt { get; set; }
}
