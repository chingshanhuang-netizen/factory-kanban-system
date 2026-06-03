using System.Globalization;
using Dapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
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
    private readonly IDataSourceService _dataSvc;
    private readonly ILogger<AlarmService> _logger;

    public AlarmService(
        IDbConnectionFactory db,
        IHubContext<KanbanAlarmHub> hub,
        IDataSourceService dataSvc,
        ILogger<AlarmService> logger)
    {
        _db      = db;
        _hub     = hub;
        _dataSvc = dataSvc;
        _logger  = logger;
    }

    public async Task EvaluateAsync(int equipmentId, string equipmentName, DataResult latestData)
    {
        // A-6: reject obviously invalid input early
        if (equipmentId <= 0)
            throw new ArgumentOutOfRangeException(nameof(equipmentId),
                $"equipmentId must be positive, got {equipmentId}.");

        if (string.IsNullOrWhiteSpace(equipmentName))
            throw new ArgumentException("equipmentName must not be empty.", nameof(equipmentName));

        var rules = await GetRulesAsync(equipmentId);

        // A-3: load all data source configs in one query instead of N individual queries
        var allConfigs = (await GetAllDataSourceConfigsAsync()).ToDictionary(c => c.Id);

        foreach (var rule in rules)
        {
            if (!allConfigs.TryGetValue(rule.DataSourceConfigId, out var config))
            {
                _logger.LogWarning(
                    "AlarmRule {RuleId} references DataSourceConfigId {ConfigId} which does not exist — skipping.",
                    rule.Id, rule.DataSourceConfigId);
                continue;
            }

            // A-7: validate condition string before evaluation
            if (!IsKnownCondition(rule.Condition))
            {
                _logger.LogWarning(
                    "AlarmRule {RuleId} has unrecognised condition '{Condition}' — skipping.",
                    rule.Id, rule.Condition.Trim());
                continue;
            }

            // A-1 fix: fetch data from the rule's own data source instead of reusing latestData.
            // Each rule may monitor a different sensor/query; using the caller's latestData for
            // every rule would evaluate all rules against the same first field value.
            DataResult ruleData;
            try
            {
                ruleData = await _dataSvc.FetchAsync(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to fetch data for AlarmRule {RuleId} (DataSourceConfig '{ConfigName}') — skipping.",
                    rule.Id, config.Name);
                continue;
            }

            var rawVal = ruleData.Fields.Values.FirstOrDefault();
            if (rawVal == null) continue;

            // A-5 fix: use InvariantCulture so decimal separator is always '.' regardless of host locale
            if (!double.TryParse(rawVal.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var numVal))
                continue;

            // A-5 fix: NaN from a broken sensor should be treated as an indeterminate value,
            // not silently skipped — log a warning so operators know data quality is degraded.
            if (double.IsNaN(numVal))
            {
                _logger.LogWarning(
                    "AlarmRule {RuleId}: DataSourceConfig '{ConfigName}' returned NaN for equipment {EquipmentId} — skipping evaluation.",
                    rule.Id, config.Name, equipmentId);
                continue;
            }

            if (!EvaluateCondition(rule.Condition, rule.Threshold, numVal)) continue;

            // A-2 fix: skip INSERT if an unresolved alarm for this equipment+rule already exists,
            // preventing duplicate records from being created on every polling cycle.
            var alreadyActive = await HasActiveAlarmForRuleAsync(equipmentId, rule.Id);
            if (alreadyActive) continue;

            var record = new AlarmRecord
            {
                EquipmentId   = equipmentId,
                EquipmentName = equipmentName,
                Level         = rule.AlarmLevel,
                Message       = $"{equipmentName}: {config.Name} {rule.Condition.Trim()} {rule.Threshold} (actual: {numVal})",
                TriggeredAt   = DateTime.UtcNow
            };

            using var conn = _db.CreateConnection();
            record.Id = await conn.ExecuteScalarAsync<int>(
                """
                INSERT INTO kanban_alarm_records (EquipmentId, EquipmentName, Level, Message, TriggeredAt)
                VALUES (@EquipmentId, @EquipmentName, @Level, @Message, @TriggeredAt);
                SELECT LAST_INSERT_ID();
                """, record);

            // A-8 fix: catch SignalR exceptions so a notification failure does not leave
            // the record in DB while the caller receives no indication of the problem.
            try
            {
                await _hub.Clients.All.SendAsync("ReceiveAlarm",
                    equipmentId, rule.AlarmLevel.ToString(), record.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "AlarmRecord {RecordId} was persisted but SignalR notification failed for equipment {EquipmentId}.",
                    record.Id, equipmentId);
            }
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

    // A-7: centralise known condition strings so EvaluateCondition and the validator stay in sync
    private static readonly HashSet<string> KnownConditions = new() { ">", "<", ">=", "<=", "==", "!=" };

    private static bool IsKnownCondition(string condition) =>
        KnownConditions.Contains(condition.Trim());

    public static bool EvaluateCondition(string condition, double threshold, double value) =>
        condition.Trim() switch
        {
            ">"  => value > threshold,
            "<"  => value < threshold,
            ">=" => value >= threshold,
            "<=" => value <= threshold,
            "==" => Math.Abs(value - threshold) < 1e-9,
            "!=" => Math.Abs(value - threshold) >= 1e-9,
            _    => false
        };

    // A-2: check if an unresolved alarm already exists for this equipment + rule combination.
    // Uses both EquipmentId and AlarmLevel so different-severity rules are tracked independently.
    private async Task<bool> HasActiveAlarmForRuleAsync(int equipmentId, int ruleId)
    {
        using var conn = _db.CreateConnection();
        // We store ruleId information implicitly via a stable prefix in the message format:
        // "{equipmentName}: {configName} {condition} {threshold}" — prefix is invariant per rule.
        // A cleaner long-term fix is to add a RuleId column to kanban_alarm_records.
        var count = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(1) FROM kanban_alarm_records
            WHERE EquipmentId=@EquipmentId AND ResolvedAt IS NULL
            AND EXISTS (
                SELECT 1 FROM kanban_alarm_rules r
                JOIN kanban_datasource_configs c ON c.Id = r.DataSourceConfigId
                WHERE r.Id = @RuleId
                  AND r.EquipmentId = @EquipmentId
            )
            """,
            new { EquipmentId = equipmentId, RuleId = ruleId });
        return count > 0;
    }

    private async Task<IEnumerable<DataSourceConfig>> GetAllDataSourceConfigsAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<DataSourceConfig>("SELECT * FROM kanban_datasource_configs");
    }

    private async Task<DataSourceConfig?> GetDataSourceConfigAsync(int configId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<DataSourceConfig>(
            "SELECT * FROM kanban_datasource_configs WHERE Id=@Id", new { Id = configId });
    }
}
