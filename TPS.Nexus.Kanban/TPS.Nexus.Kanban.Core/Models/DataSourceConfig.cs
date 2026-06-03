using TPS.Nexus.Kanban.Core.Enums;

namespace TPS.Nexus.Kanban.Core.Models;

public class DataSourceConfig
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DataSourceType SourceType { get; set; }
    public string? ConnectionString { get; set; }
    public string? FilePath { get; set; }
    public string? QueryOrPath { get; set; }
    public string? Parameters { get; set; }
}
