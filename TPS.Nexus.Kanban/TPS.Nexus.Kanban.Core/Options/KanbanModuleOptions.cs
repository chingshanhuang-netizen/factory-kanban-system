namespace TPS.Nexus.Kanban.Core.Options;

public class KanbanModuleOptions
{
    public int  MapPollIntervalSeconds      { get; set; } = 30;
    public int  HistoryChartMaxRows         { get; set; } = 20;
    public int  TooltipHistoryMaxRows       { get; set; } = 10;
    public long IconUploadMaxBytes          { get; set; } = 5 * 1024 * 1024;
    public long MapUploadMaxBytes           { get; set; } = 50 * 1024 * 1024;
    public int  DefaultWidgetRefreshSecs    { get; set; } = 30;
    public int  AlarmToastDurationMs        { get; set; } = 5000;
    public int  TooltipHistoryWindowHours   { get; set; } = 1;
    public int  TooltipFieldsMaxCount       { get; set; } = 6;
    public int  TabDataColumnsMaxCount      { get; set; } = 5;
}
