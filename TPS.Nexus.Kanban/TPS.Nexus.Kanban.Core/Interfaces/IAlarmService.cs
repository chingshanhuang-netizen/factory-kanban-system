using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Core.Interfaces;

public interface IAlarmService
{
    Task EvaluateAsync(int equipmentId, string equipmentName, DataResult latestData);
    Task<IEnumerable<AlarmRecord>> GetActiveAlarmsAsync();
    Task ResolveAlarmAsync(int alarmRecordId);
    Task<IEnumerable<AlarmRule>> GetRulesAsync(int equipmentId);
    Task SaveRuleAsync(AlarmRule rule);
    Task DeleteRuleAsync(int ruleId);
}
