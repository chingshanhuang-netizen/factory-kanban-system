using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Interfaces;
using TPS.Nexus.Kanban.Core.Models;
using EquipmentModel = TPS.Nexus.Kanban.Core.Models.Equipment;

namespace TPS.Nexus.Kanban.Demo.Mocks;

public class DemoEquipmentService : IEquipmentService
{
    private readonly List<EquipmentModel> _equipment = new()
    {
        new() { Id=1, Name="CNC加工機",  Category="加工設備", Tag="CNC-01", Description="5軸加工中心",    IconType=IconType.CssClass },
        new() { Id=2, Name="組裝機器人", Category="組裝設備", Tag="ASM-01", Description="六軸工業機器人",   IconType=IconType.CssClass },
        new() { Id=3, Name="品質檢測站", Category="檢測設備", Tag="QC-01",  Description="3D視覺量測系統",   IconType=IconType.CssClass },
    };

    // Widgets placed at positions matching the three zones in demo-floor.svg
    private readonly List<EquipmentWidget> _widgets = new()
    {
        new() { Id=1, EquipmentId=1, LayoutVersionId=3, PositionX=130, PositionY=230, Width=100, Height=120 },
        new() { Id=2, EquipmentId=2, LayoutVersionId=3, PositionX=460, PositionY=230, Width=100, Height=120 },
        new() { Id=3, EquipmentId=3, LayoutVersionId=3, PositionX=790, PositionY=230, Width=100, Height=120 },
    };

    private readonly List<DataSourceConfig> _dataSources = new()
    {
        new() { Id=1, Name="CNC 主軸溫度",   SourceType=DataSourceType.Csv, FilePath="/data/cnc-temp.csv"   },
        new() { Id=2, Name="機器人電流感測", SourceType=DataSourceType.Csv, FilePath="/data/robot-amp.csv"  },
        new() { Id=3, Name="檢測站產量計數", SourceType=DataSourceType.Json, FilePath="/data/qc-count.json" },
    };

    public Task<IEnumerable<EquipmentModel>> GetAllEquipmentAsync() =>
        Task.FromResult(_equipment.AsEnumerable());

    public Task<EquipmentModel?> GetEquipmentAsync(int id) =>
        Task.FromResult(_equipment.FirstOrDefault(e => e.Id == id));

    public Task<EquipmentModel> CreateEquipmentAsync(EquipmentModel equipment)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        if (string.IsNullOrWhiteSpace(equipment.Name))
            throw new ArgumentException("設備名稱不能為空。", nameof(equipment));
        equipment.Id = _equipment.Count > 0 ? _equipment.Max(e => e.Id) + 1 : 1;
        _equipment.Add(equipment);
        return Task.FromResult(equipment);
    }

    public Task UpdateEquipmentAsync(EquipmentModel equipment)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        if (equipment.Id <= 0) throw new ArgumentOutOfRangeException(nameof(equipment));
        var idx = _equipment.FindIndex(e => e.Id == equipment.Id);
        if (idx >= 0) _equipment[idx] = equipment;
        return Task.CompletedTask;
    }

    public Task DeleteEquipmentAsync(int id)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        _equipment.RemoveAll(e => e.Id == id);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<EquipmentWidget>> GetWidgetsByVersionAsync(int layoutVersionId)
    {
        var result = _widgets.Where(w => w.LayoutVersionId == layoutVersionId).ToList();
        return Task.FromResult(result.AsEnumerable());
    }

    public Task<EquipmentWidget> SaveWidgetAsync(EquipmentWidget widget)
    {
        if (widget.EquipmentId <= 0) throw new ArgumentOutOfRangeException(nameof(widget));
        if (widget.LayoutVersionId <= 0) throw new ArgumentOutOfRangeException(nameof(widget));
        var idx = _widgets.FindIndex(w => w.Id == widget.Id);
        if (idx >= 0) _widgets[idx] = widget;
        else { widget.Id = _widgets.Count > 0 ? _widgets.Max(w => w.Id) + 1 : 1; _widgets.Add(widget); }
        return Task.FromResult(widget);
    }

    public Task DeleteWidgetAsync(int widgetId)
    {
        if (widgetId <= 0) throw new ArgumentOutOfRangeException(nameof(widgetId));
        _widgets.RemoveAll(w => w.Id == widgetId);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<WidgetComponent>> GetComponentsByWidgetAsync(int widgetId) =>
        Task.FromResult(Enumerable.Empty<WidgetComponent>());

    public Task SaveComponentAsync(WidgetComponent component)
    {
        if (component.RefreshInterval <= 0) throw new ArgumentOutOfRangeException(nameof(component));
        return Task.CompletedTask;
    }

    public Task DeleteComponentAsync(int componentId)
    {
        if (componentId <= 0) throw new ArgumentOutOfRangeException(nameof(componentId));
        return Task.CompletedTask;
    }

    public Task<IEnumerable<EquipmentLinkConfig>> GetLinkConfigsAsync(int equipmentId) =>
        Task.FromResult(Enumerable.Empty<EquipmentLinkConfig>());

    public Task SaveLinkConfigAsync(EquipmentLinkConfig config) => Task.CompletedTask;

    public Task DeleteLinkConfigAsync(int id)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        return Task.CompletedTask;
    }

    public Task<IEnumerable<DataSourceConfig>> GetAllDataSourceConfigsAsync() =>
        Task.FromResult(_dataSources.AsEnumerable());

    public Task<DataSourceConfig> SaveDataSourceConfigAsync(DataSourceConfig config)
    {
        if (config.Id == 0)
        {
            config.Id = _dataSources.Count > 0 ? _dataSources.Max(c => c.Id) + 1 : 1;
            _dataSources.Add(config);
        }
        else
        {
            var idx = _dataSources.FindIndex(c => c.Id == config.Id);
            if (idx >= 0) _dataSources[idx] = config;
        }
        return Task.FromResult(config);
    }

    public Task DeleteDataSourceConfigAsync(int id)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        _dataSources.RemoveAll(c => c.Id == id);
        return Task.CompletedTask;
    }
}
