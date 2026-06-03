using TPS.Nexus.Kanban.Core.Enums;

namespace TPS.Nexus.Kanban.Core.Models;

public class AlarmRecord
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public string EquipmentName { get; set; } = string.Empty;
    public AlarmLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime TriggeredAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
