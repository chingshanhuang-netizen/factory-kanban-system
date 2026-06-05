using TPS.Nexus.Kanban.Core.Enums;

namespace TPS.Nexus.Kanban.Core.Models;

public class Equipment
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Tag { get; set; }
    public string? Description { get; set; }
    public string? MapName { get; set; }
    public IconType IconType { get; set; }
    public string? IconValue { get; set; }
}
