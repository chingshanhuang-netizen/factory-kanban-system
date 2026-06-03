using TPS.Nexus.Kanban.Core.Enums;

namespace TPS.Nexus.Kanban.Core.Models;

public class LayoutVersion
{
    public int Id { get; set; }
    public int FactoryMapId { get; set; }
    public int VersionNo { get; set; }
    public LayoutStatus Status { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public string? LayoutJson { get; set; }
}
