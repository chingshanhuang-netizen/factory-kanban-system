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
    // AS-1: tracks which rule triggered this record for per-rule dedup in HasActiveAlarmForRuleAsync.
    // Requires: ALTER TABLE kanban_alarm_records ADD COLUMN AlarmRuleId INT NULL;
    public int? AlarmRuleId { get; set; }
}
