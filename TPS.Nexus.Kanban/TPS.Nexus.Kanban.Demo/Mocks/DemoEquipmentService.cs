using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Interfaces;
using TPS.Nexus.Kanban.Core.Models;
using EquipmentModel = TPS.Nexus.Kanban.Core.Models.Equipment;

namespace TPS.Nexus.Kanban.Demo.Mocks;

public class DemoEquipmentService : IEquipmentService
{
    private readonly List<EquipmentModel> _equipment = new()
    {
        new() { Id=1,  Name="CNC加工機",    Category="加工設備", Tag="CNC-01",  Description="5軸加工中心",      MapName="廠區一樓平面圖", IconType=IconType.CssClass },
        new() { Id=2,  Name="組裝機器人",   Category="組裝設備", Tag="ASM-01",  Description="六軸工業機器人",   MapName="廠區一樓平面圖", IconType=IconType.CssClass },
        new() { Id=3,  Name="品質檢測站",   Category="檢測設備", Tag="QC-01",   Description="3D視覺量測系統",   MapName="廠區一樓平面圖", IconType=IconType.CssClass },
        new() { Id=4,  Name="雷射切割機",   Category="加工設備", Tag="LSR-01",  Description="CO₂雷射切割",      MapName="廠區一樓平面圖", IconType=IconType.CssClass },
        new() { Id=5,  Name="沖壓成型機",   Category="加工設備", Tag="PRS-01",  Description="100T油壓沖床",     MapName="廠區一樓平面圖", IconType=IconType.CssClass },
        new() { Id=6,  Name="焊接機器人",   Category="組裝設備", Tag="WLD-01",  Description="MIG/MAG自動焊接",  MapName="廠區一樓平面圖", IconType=IconType.CssClass },
        new() { Id=7,  Name="輸送帶系統",   Category="物流設備", Tag="CVY-01",  Description="主線輸送皮帶",     MapName="廠區一樓平面圖", IconType=IconType.CssClass },
        new() { Id=8,  Name="AGV搬運車",    Category="物流設備", Tag="AGV-01",  Description="自動導引搬運車",   MapName="廠區一樓平面圖", IconType=IconType.CssClass },
        new() { Id=9,  Name="噴塗機器人",   Category="表面處理", Tag="SPR-01",  Description="六軸自動噴漆臂",   MapName="廠區二樓平面圖", IconType=IconType.CssClass },
        new() { Id=10, Name="烤漆烘烤爐",   Category="表面處理", Tag="OVN-01",  Description="紅外線烘烤隧道爐", MapName="廠區二樓平面圖", IconType=IconType.CssClass },
        new() { Id=11, Name="壓縮空氣站",   Category="公用設施", Tag="AIR-01",  Description="螺旋式空壓機組",   MapName="廠區二樓平面圖", IconType=IconType.CssClass },
        new() { Id=12, Name="冷卻水塔",     Category="公用設施", Tag="CLT-01",  Description="循環冷卻水系統",   MapName="廠區二樓平面圖", IconType=IconType.CssClass },
        new() { Id=13, Name="CMM座標量測機", Category="檢測設備", Tag="CMM-01",  Description="三次元座標量測儀", MapName="廠區二樓平面圖", IconType=IconType.CssClass },
        new() { Id=14, Name="X光探傷儀",    Category="檢測設備", Tag="XRY-01",  Description="工業X射線檢測",    MapName="廠區二樓平面圖", IconType=IconType.CssClass },
        new() { Id=15, Name="廢水處理站",   Category="環保設備", Tag="WWT-01",  Description="工業廢水過濾系統", MapName="廠區二樓平面圖", IconType=IconType.CssClass },
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
        new() { Id=1, Name="CNC 主軸溫度",   SourceType=DataSourceType.Csv,  FilePath="/data/cnc-temp.csv",   DataType="溫度" },
        new() { Id=2, Name="機器人電流感測", SourceType=DataSourceType.Csv,  FilePath="/data/robot-amp.csv",  DataType="電流" },
        new() { Id=3, Name="檢測站產量計數", SourceType=DataSourceType.Json, FilePath="/data/qc-count.json",  DataType="計數" },
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

    public Task<bool> IsEquipmentInUseAsync(int equipmentId) =>
        Task.FromResult(_widgets.Any(w => w.EquipmentId == equipmentId));

    public Task<bool> IsDataSourceInUseAsync(int dataSourceConfigId) =>
        Task.FromResult(false);
}
