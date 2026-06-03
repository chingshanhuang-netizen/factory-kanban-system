using TPS.Nexus.Kanban.Core.Enums;

namespace TPS.Nexus.Kanban.Core.Models;

public class AlarmRule
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public int DataSourceConfigId { get; set; }
    public string Condition { get; set; } = string.Empty;
    public double Threshold { get; set; }
    public AlarmLevel AlarmLevel { get; set; }
}
