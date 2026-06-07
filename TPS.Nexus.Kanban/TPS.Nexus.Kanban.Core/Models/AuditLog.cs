namespace TPS.Nexus.Kanban.Core.Models;

public class AuditLog
{
    public int      Id          { get; set; }
    public string   EntityType  { get; set; } = string.Empty;
    public int      EntityId    { get; set; }
    public string   EntityName  { get; set; } = string.Empty;
    public string   Action      { get; set; } = "DELETE";
    public string   PerformedBy { get; set; } = string.Empty;
    public DateTime PerformedAt { get; set; }
    public string?  Details     { get; set; }
}
