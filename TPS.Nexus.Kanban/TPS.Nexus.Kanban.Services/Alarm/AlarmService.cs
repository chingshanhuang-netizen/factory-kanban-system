using TPS.Nexus.Kanban.Core.Interfaces;
using TPS.Nexus.Kanban.Core.Models;
namespace TPS.Nexus.Kanban.Services.Alarm;
public class AlarmService : IAlarmService
{
    public Task EvaluateAsync(int equipmentId, string equipmentName, DataResult latestData) => throw new NotImplementedException();
    public Task<IEnumerable<AlarmRecord>> GetActiveAlarmsAsync() => throw new NotImplementedException();
    public Task ResolveAlarmAsync(int alarmRecordId) => throw new NotImplementedException();
    public Task<IEnumerable<AlarmRule>> GetRulesAsync(int equipmentId) => throw new NotImplementedException();
    public Task SaveRuleAsync(AlarmRule rule) => throw new NotImplementedException();
    public Task DeleteRuleAsync(int ruleId) => throw new NotImplementedException();
}
