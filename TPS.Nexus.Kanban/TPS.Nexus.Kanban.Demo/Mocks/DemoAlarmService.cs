using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Interfaces;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Demo.Mocks;

public class DemoAlarmService : IAlarmService
{
    private readonly List<AlarmRecord> _active = new()
    {
        new()
        {
            Id            = 1,
            EquipmentId   = 1,
            EquipmentName = "CNC加工機",
            Level         = AlarmLevel.Warning,
            Message       = "CNC加工機: 主軸溫度 > 80.0 (actual: 87.3 ℃)",
            TriggeredAt   = DateTime.UtcNow.AddMinutes(-35),
            AlarmRuleId   = 1,
        },
    };

    public Task EvaluateAsync(int equipmentId, string equipmentName) => Task.CompletedTask;

    public Task<IEnumerable<AlarmRecord>> GetActiveAlarmsAsync() =>
        Task.FromResult(_active.AsEnumerable());

    public Task ResolveAlarmAsync(int alarmRecordId)
    {
        _active.RemoveAll(a => a.Id == alarmRecordId);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<AlarmRule>> GetRulesAsync(int equipmentId) =>
        Task.FromResult(Enumerable.Empty<AlarmRule>());

    public Task SaveRuleAsync(AlarmRule rule) => Task.CompletedTask;
    public Task DeleteRuleAsync(int ruleId) => Task.CompletedTask;
}
