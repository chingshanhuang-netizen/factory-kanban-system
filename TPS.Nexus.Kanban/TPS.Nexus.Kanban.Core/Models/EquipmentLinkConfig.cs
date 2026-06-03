using TPS.Nexus.Kanban.Core.Enums;

namespace TPS.Nexus.Kanban.Core.Models;

public class EquipmentLinkConfig
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public LinkType LinkType { get; set; }
    public string TabLabel { get; set; } = string.Empty;
    public string? UrlTemplate { get; set; }
    public int? DataSourceConfigId { get; set; }
    public int DisplayOrder { get; set; }
}
