using TPS.Nexus.Kanban.Core.Enums;

namespace TPS.Nexus.Kanban.Core.Models;

public class WidgetComponent
{
    public int Id { get; set; }
    public int EquipmentWidgetId { get; set; }
    public WidgetComponentType ComponentType { get; set; }
    public int? DataSourceConfigId { get; set; }
    public string? Label { get; set; }
    public string? Unit { get; set; }
    public int RefreshInterval { get; set; } = 30;
    public int DisplayOrder { get; set; }
    public string? ConfigJson { get; set; }
}
