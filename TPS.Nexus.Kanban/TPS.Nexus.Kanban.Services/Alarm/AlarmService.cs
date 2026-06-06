using System.Globalization;
using Dapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Core.Constants;
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
    private readonly IEquipmentService _equip;
    private readonly ILogger<AlarmService> _logger;

    public AlarmService(
        IDbConnectionFactory db,
        IHubContext<KanbanAlarmHub> hub,
        IDataSourceService dataSvc,
        IEquipmentService equip,
        ILogger<AlarmService> logger)
    {
        _db      = db;
        _hub     = hub;
        _dataSvc = dataSvc;
        _equip   = equip;
        _logger  = logger;
    }

    public async Task EvaluateAsync(int equipmentId, string equipmentName)
    {
        // A-6: reject obviously invalid input early
        if (equipmentId <= 0)
            throw new ArgumentOutOfRangeException(nameof(equipmentId),
                $"equipmentId must be positive, got {equipmentId}.");

        if (string.IsNullOrWhiteSpace(equipmentName))
            throw new ArgumentException("equipmentName must not be empty.", nameof(equipmentName));

        var rules = await GetRulesAsync(equipmentId);

        // A-3: load all data source configs in one query instead of N individual queries
        var allConfigs = (await _equip.GetAllDataSourceConfigsAsync()).ToDictionary(c => c.Id);

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
                    rule.Id, rule.Condition?.Trim() ?? "(null)");
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

            // Use FieldName to look up the specific field; fall back to first field for legacy rules.
            var rawVal = ruleData.Fields.TryGetValue(rule.FieldName, out var named)
                ? named
                : ruleData.Fields.Values.FirstOrDefault();
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

            // null-safety guaranteed by IsKnownCondition check above
            if (!EvaluateCondition(rule.Condition!, rule.Threshold, numVal)) continue;

            // A-2 fix: skip INSERT if an unresolved alarm for this equipment+rule already exists,
            // preventing duplicate records from being created on every polling cycle.
            var alreadyActive = await HasActiveAlarmForRuleAsync(equipmentId, rule.Id);
            if (alreadyActive) continue;

            var record = new AlarmRecord
            {
                EquipmentId   = equipmentId,
                EquipmentName = equipmentName,
                Level         = rule.AlarmLevel,
                Message       = $"{equipmentName}: {config.Name} {rule.Condition!.Trim()} {rule.Threshold} (actual: {numVal})",
                TriggeredAt   = DateTime.UtcNow,
                AlarmRuleId   = rule.Id   // AS-1: stored for per-rule dedup
            };

            await using var conn = _db.CreateConnection();
            record.Id = await conn.ExecuteScalarAsync<int>(
                """
                INSERT INTO kanban_alarm_records (EquipmentId, EquipmentName, Level, Message, TriggeredAt, AlarmRuleId)
                VALUES (@EquipmentId, @EquipmentName, @Level, @Message, @TriggeredAt, @AlarmRuleId);
                SELECT LAST_INSERT_ID();
                """, record);

            // A-8 fix: catch SignalR exceptions so a notification failure does not leave
            // the record in DB while the caller receives no indication of the problem.
            try
            {
                await _hub.Clients.All.SendAsync(KanbanRoutes.ReceiveAlarmEvent,
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
        await using var conn = _db.CreateConnection();
        return await conn.QueryAsync<AlarmRecord>(
            "SELECT * FROM kanban_alarm_records WHERE ResolvedAt IS NULL ORDER BY TriggeredAt DESC");
    }

    public async Task ResolveAlarmAsync(int alarmRecordId)
    {
        // AS-3: reject non-positive IDs — UPDATE WHERE Id=0 silently does nothing
        if (alarmRecordId <= 0)
            throw new ArgumentOutOfRangeException(nameof(alarmRecordId),
                $"alarmRecordId must be positive, got {alarmRecordId}.");

        await using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE kanban_alarm_records SET ResolvedAt=@Now WHERE Id=@Id",
            new { Now = DateTime.UtcNow, Id = alarmRecordId });
    }

    public async Task<IEnumerable<AlarmRule>> GetRulesAsync(int equipmentId)
    {
        await using var conn = _db.CreateConnection();
        return await conn.QueryAsync<AlarmRule>(
            "SELECT * FROM kanban_alarm_rules WHERE EquipmentId=@EquipmentId AND IsEnabled=1",
            new { EquipmentId = equipmentId });
    }

    public async Task<IEnumerable<AlarmRule>> GetAllRulesAsync(int equipmentId)
    {
        await using var conn = _db.CreateConnection();
        return await conn.QueryAsync<AlarmRule>(
            "SELECT * FROM kanban_alarm_rules WHERE EquipmentId=@EquipmentId",
            new { EquipmentId = equipmentId });
    }

    public async Task SaveRuleAsync(AlarmRule rule)
    {
        // AS-2: validate all fields before touching the DB to prevent dead rules that are
        // silently skipped on every evaluation cycle.
        if (rule.EquipmentId <= 0)
            throw new ArgumentOutOfRangeException(nameof(rule),
                $"AlarmRule.EquipmentId must be positive, got {rule.EquipmentId}.");

        if (rule.DataSourceConfigId <= 0)
            throw new ArgumentOutOfRangeException(nameof(rule),
                $"AlarmRule.DataSourceConfigId must be positive, got {rule.DataSourceConfigId}.");

        if (!IsKnownCondition(rule.Condition))
            throw new ArgumentException(
                $"AlarmRule.Condition '{rule.Condition}' is not a supported operator. " +
                $"Valid operators: {string.Join(", ", KnownConditions)}.", nameof(rule));

        if (double.IsNaN(rule.Threshold) || double.IsInfinity(rule.Threshold))
            throw new ArgumentException(
                $"AlarmRule.Threshold must be a finite number, got {rule.Threshold}.", nameof(rule));

        await using var conn = _db.CreateConnection();
        if (rule.Id == 0)
            await conn.ExecuteAsync(
                """
                INSERT INTO kanban_alarm_rules
                  (EquipmentId, DataSourceConfigId, FieldName, Condition, Threshold, AlarmLevel, Message, IsEnabled)
                VALUES
                  (@EquipmentId, @DataSourceConfigId, @FieldName, @Condition, @Threshold, @AlarmLevel, @Message, @IsEnabled)
                """, rule);
        else
            await conn.ExecuteAsync(
                """
                UPDATE kanban_alarm_rules
                SET FieldName=@FieldName, Condition=@Condition, Threshold=@Threshold,
                    AlarmLevel=@AlarmLevel, Message=@Message, IsEnabled=@IsEnabled
                WHERE Id=@Id
                """, rule);
    }

    public async Task DeleteRuleAsync(int ruleId)
    {
        // AS-3: reject non-positive IDs
        if (ruleId <= 0)
            throw new ArgumentOutOfRangeException(nameof(ruleId),
                $"ruleId must be positive, got {ruleId}.");

        await using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM kanban_alarm_rules WHERE Id=@Id", new { Id = ruleId });
    }

    // A-7: centralise known condition strings so EvaluateCondition and the validator stay in sync
    private static readonly HashSet<string> KnownConditions = new() { ">", "<", ">=", "<=", "==", "!=" };

    // Precision boundary for floating-point equality checks on alarm thresholds.
    // Sensor readings within this tolerance of the threshold are treated as equal.
    private const double EqualityEpsilon = 1e-9;

    // null guard: Dapper can map a NULL DB column to null even when the model property has a
    // default initialiser (= string.Empty), so treat null as an unknown/invalid condition.
    private static bool IsKnownCondition(string? condition) =>
        condition != null && KnownConditions.Contains(condition.Trim());

    public static bool EvaluateCondition(string condition, double threshold, double value) =>
        condition.Trim() switch
        {
            ">"  => value > threshold,
            "<"  => value < threshold,
            ">=" => value >= threshold,
            "<=" => value <= threshold,
            "==" => Math.Abs(value - threshold) < EqualityEpsilon,
            "!=" => Math.Abs(value - threshold) >= EqualityEpsilon,
            _    => false
        };

    // AS-1 fix: check if an unresolved alarm for THIS SPECIFIC RULE already exists.
    // The old EXISTS subquery only confirmed the rule+config existed, not that the active alarm
    // belonged to that rule — causing an equipment-level lock instead of a per-rule dedup.
    // Now uses the AlarmRuleId column added to kanban_alarm_records for exact matching.
    private async Task<bool> HasActiveAlarmForRuleAsync(int equipmentId, int ruleId)
    {
        await using var conn = _db.CreateConnection();
        var count = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(1) FROM kanban_alarm_records
            WHERE EquipmentId=@EquipmentId AND AlarmRuleId=@RuleId AND ResolvedAt IS NULL
            """,
            new { EquipmentId = equipmentId, RuleId = ruleId });
        return count > 0;
    }

}
