using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Models;
using Xunit;
using EquipmentModel = TPS.Nexus.Kanban.Core.Models.Equipment;

namespace TPS.Nexus.Kanban.Tests.Equipment;

public class EquipmentServiceTests
{
    [Fact]
    public void EquipmentWidget_DefaultDimensions_Are80x100()
    {
        var widget = new EquipmentWidget();
        Assert.Equal(80, widget.Width);
        Assert.Equal(100, widget.Height);
    }

    [Fact]
    public void EquipmentWidget_Components_InitializesAsEmpty()
    {
        var widget = new EquipmentWidget();
        Assert.NotNull(widget.Components);
        Assert.Empty(widget.Components);
    }

    [Fact]
    public void EquipmentWidget_CanAccumulateComponents()
    {
        var widget = new EquipmentWidget { Id = 1 };
        widget.Components.Add(new WidgetComponent { ComponentType = WidgetComponentType.StatusIndicator, DisplayOrder = 0 });
        widget.Components.Add(new WidgetComponent { ComponentType = WidgetComponentType.ValueGauge,      DisplayOrder = 1 });
        widget.Components.Add(new WidgetComponent { ComponentType = WidgetComponentType.TrendChart,      DisplayOrder = 2 });
        Assert.Equal(3, widget.Components.Count);
    }

    [Fact]
    public void WidgetComponent_HasDefaultRefreshInterval_30()
    {
        var component = new WidgetComponent();
        Assert.Equal(30, component.RefreshInterval);
    }

    [Fact]
    public void Equipment_OptionalFields_DefaultToNull()
    {
        var equipment = new EquipmentModel { Name = "Machine A" };
        Assert.Null(equipment.Tag);
        Assert.Null(equipment.Description);
        Assert.Null(equipment.IconValue);
    }
}
