using NSubstitute;
using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Models;
using TPS.Nexus.Kanban.Services.Equipment;
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

    // ── Helper ────────────────────────────────────────────────────────────────

    private static EquipmentService CreateService()
    {
        var db = Substitute.For<IDbConnectionFactory>();
        return new EquipmentService(db);
    }

    // ── ES-3: SaveWidgetAsync — FK guards ─────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SaveWidgetAsync_NonPositiveEquipmentId_ThrowsArgumentOutOfRange(int equipmentId)
    {
        var svc    = CreateService();
        var widget = new EquipmentWidget { EquipmentId = equipmentId, LayoutVersionId = 1 };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => svc.SaveWidgetAsync(widget));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SaveWidgetAsync_NonPositiveLayoutVersionId_ThrowsArgumentOutOfRange(int versionId)
    {
        var svc    = CreateService();
        var widget = new EquipmentWidget { EquipmentId = 1, LayoutVersionId = versionId };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => svc.SaveWidgetAsync(widget));
    }

    // ── ES-5: SaveComponentAsync — RefreshInterval guard ──────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-30)]
    public async Task SaveComponentAsync_NonPositiveRefreshInterval_ThrowsArgumentOutOfRange(int interval)
    {
        var svc       = CreateService();
        var component = new WidgetComponent { EquipmentWidgetId = 1, RefreshInterval = interval };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => svc.SaveComponentAsync(component));
    }

    // ── ES-4: Delete* guards ──────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task DeleteEquipmentAsync_NonPositiveId_ThrowsArgumentOutOfRange(int id)
    {
        var svc = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => svc.DeleteEquipmentAsync(id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task DeleteWidgetAsync_NonPositiveId_ThrowsArgumentOutOfRange(int id)
    {
        var svc = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => svc.DeleteWidgetAsync(id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task DeleteComponentAsync_NonPositiveId_ThrowsArgumentOutOfRange(int id)
    {
        var svc = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => svc.DeleteComponentAsync(id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task DeleteLinkConfigAsync_NonPositiveId_ThrowsArgumentOutOfRange(int id)
    {
        var svc = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => svc.DeleteLinkConfigAsync(id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task DeleteDataSourceConfigAsync_NonPositiveId_ThrowsArgumentOutOfRange(int id)
    {
        var svc = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => svc.DeleteDataSourceConfigAsync(id));
    }

    // ── S-1: CreateEquipmentAsync — null equipment guard ─────────────────────

    [Fact]
    public async Task CreateEquipmentAsync_NullEquipment_ThrowsArgumentNullException()
    {
        var svc = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => svc.CreateEquipmentAsync(null!));
    }

    // ── S-1: CreateEquipmentAsync — empty Name guard ──────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateEquipmentAsync_EmptyName_ThrowsArgumentException(string name)
    {
        var svc       = CreateService();
        var equipment = new EquipmentModel { Name = name };

        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.CreateEquipmentAsync(equipment));
    }

    // ── S-2: UpdateEquipmentAsync — null and zero-Id guards ──────────────────

    [Fact]
    public async Task UpdateEquipmentAsync_NullEquipment_ThrowsArgumentNullException()
    {
        var svc = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => svc.UpdateEquipmentAsync(null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task UpdateEquipmentAsync_NonPositiveId_ThrowsArgumentOutOfRange(int id)
    {
        var svc       = CreateService();
        var equipment = new EquipmentModel { Id = id, Name = "Machine" };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => svc.UpdateEquipmentAsync(equipment));
    }

    // ── S-3: SaveLinkConfigAsync — null config guard ──────────────────────────

    [Fact]
    public async Task SaveLinkConfigAsync_NullConfig_ThrowsArgumentNullException()
    {
        var svc = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => svc.SaveLinkConfigAsync(null!));
    }

    // ── S-3: SaveLinkConfigAsync — non-positive EquipmentId on INSERT guard ───

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SaveLinkConfigAsync_InsertWithNonPositiveEquipmentId_ThrowsArgumentOutOfRange(int equipmentId)
    {
        var svc    = CreateService();
        var config = new EquipmentLinkConfig { Id = 0, EquipmentId = equipmentId }; // Id=0 triggers INSERT path

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => svc.SaveLinkConfigAsync(config));
    }
}
