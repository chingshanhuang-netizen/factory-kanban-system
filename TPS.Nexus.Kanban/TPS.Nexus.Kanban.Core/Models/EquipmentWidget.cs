namespace TPS.Nexus.Kanban.Core.Models;

public class EquipmentWidget
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public int LayoutVersionId { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public int Width { get; set; } = 80;
    public int Height { get; set; } = 100;
    public List<WidgetComponent> Components { get; set; } = new();
}
