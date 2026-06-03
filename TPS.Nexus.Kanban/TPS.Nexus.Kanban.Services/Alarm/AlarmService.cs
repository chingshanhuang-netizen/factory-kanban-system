using Dapper;
using Microsoft.AspNetCore.SignalR;
using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Interfaces;
using TPS.Nexus.Kanban.Core.Models;
using TPS.Nexus.Kanban.Services.Hubs;

namespace TPS.Nexus.Kanban.Services.Alarm;

public class AlarmService : IAlarmService
{
    private readonly IDbConnectionFactory _db;
    private readonly IHubContext<KanbanAlarmHub> _hub;

    public AlarmService(IDbConnectionFactory db, IHubContext<KanbanAlarmHub> hub)
    {
        _db  = db;
        _hub = hub;
    }

    public async Task EvaluateAsync(int equipmentId, string equipmentName, DataResult latestData)
    {
        var rules = await GetRulesAsync(equipmentId);
        foreach (var rule in rules)
        {
            var config = await GetDataSourceConfigAsync(rule.DataSourceConfigId);
            if (config == null) continue;

            var rawVal = latestData.Fields.Values.FirstOrDefault();
            if (rawVal == null) continue;
            if (!double.TryParse(rawVal.ToString(), out var numVal)) continue;

            if (!EvaluateCondition(rule.Condition, rule.Threshold, numVal)) continue;

            var record = new AlarmRecord
            {
                EquipmentId   = equipmentId,
                EquipmentName = equipmentName,
                Level         = rule.AlarmLevel,
                Message       = $"{equipmentName}: {config.Name} {rule.Condition} {rule.Threshold} (actual: {numVal})",
                TriggeredAt   = DateTime.UtcNow
            };

            using var conn = _db.CreateConnection();
            record.Id = await conn.ExecuteScalarAsync<int>(
                """
                INSERT INTO kanban_alarm_records (EquipmentId, EquipmentName, Level, Message, TriggeredAt)
                VALUES (@EquipmentId, @EquipmentName, @Level, @Message, @TriggeredAt);
                SELECT LAST_INSERT_ID();
                """, record);

            await _hub.Clients.All.SendAsync("ReceiveAlarm",
                equipmentId, rule.AlarmLevel.ToString(), record.Message);
        }
    }

    public async Task<IEnumerable<AlarmRecord>> GetActiveAlarmsAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<AlarmRecord>(
            "SELECT * FROM kanban_alarm_records WHERE ResolvedAt IS NULL ORDER BY TriggeredAt DESC");
    }

    public async Task ResolveAlarmAsync(int alarmRecordId)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE kanban_alarm_records SET ResolvedAt=@Now WHERE Id=@Id",
            new { Now = DateTime.UtcNow, Id = alarmRecordId });
    }

    public async Task<IEnumerable<AlarmRule>> GetRulesAsync(int equipmentId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<AlarmRule>(
            "SELECT * FROM kanban_alarm_rules WHERE EquipmentId=@EquipmentId",
            new { EquipmentId = equipmentId });
    }

    public async Task SaveRuleAsync(AlarmRule rule)
    {
        using var conn = _db.CreateConnection();
        if (rule.Id == 0)
            await conn.ExecuteAsync(
                """
                INSERT INTO kanban_alarm_rules (EquipmentId, DataSourceConfigId, Condition, Threshold, AlarmLevel)
                VALUES (@EquipmentId, @DataSourceConfigId, @Condition, @Threshold, @AlarmLevel)
                """, rule);
        else
            await conn.ExecuteAsync(
                "UPDATE kanban_alarm_rules SET Condition=@Condition, Threshold=@Threshold, AlarmLevel=@AlarmLevel WHERE Id=@Id",
                rule);
    }

    public async Task DeleteRuleAsync(int ruleId)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM kanban_alarm_rules WHERE Id=@Id", new { Id = ruleId });
    }

    public static bool EvaluateCondition(string condition, double threshold, double value) => condition switch
    {
        ">"  => value > threshold,
        "<"  => value < threshold,
        ">=" => value >= threshold,
        "<=" => value <= threshold,
        "==" => Math.Abs(value - threshold) < 1e-9,
        "!=" => Math.Abs(value - threshold) >= 1e-9,
        _    => false
    };

    private async Task<DataSourceConfig?> GetDataSourceConfigAsync(int configId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<DataSourceConfig>(
            "SELECT * FROM kanban_datasource_configs WHERE Id=@Id", new { Id = configId });
    }
}
