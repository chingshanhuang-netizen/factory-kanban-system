using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Core.Interfaces;

public interface IEquipmentService
{
    Task<IEnumerable<Equipment>> GetAllEquipmentAsync();
    Task<Equipment?> GetEquipmentAsync(int id);
    Task<Equipment> CreateEquipmentAsync(Equipment equipment);
    Task UpdateEquipmentAsync(Equipment equipment);
    Task DeleteEquipmentAsync(int id);

    Task<IEnumerable<EquipmentWidget>> GetWidgetsByVersionAsync(int layoutVersionId);
    Task<EquipmentWidget> SaveWidgetAsync(EquipmentWidget widget);
    Task DeleteWidgetAsync(int widgetId);

    Task<IEnumerable<WidgetComponent>> GetComponentsByWidgetAsync(int widgetId);
    Task SaveComponentAsync(WidgetComponent component);
    Task DeleteComponentAsync(int componentId);

    Task<IEnumerable<EquipmentLinkConfig>> GetLinkConfigsAsync(int equipmentId);
    Task SaveLinkConfigAsync(EquipmentLinkConfig config);
    Task DeleteLinkConfigAsync(int id);

    Task<IEnumerable<DataSourceConfig>> GetAllDataSourceConfigsAsync();
    Task<DataSourceConfig> SaveDataSourceConfigAsync(DataSourceConfig config);
    Task DeleteDataSourceConfigAsync(int id);

    Task<bool> IsEquipmentInUseAsync(int equipmentId);
    Task<bool> IsDataSourceInUseAsync(int dataSourceConfigId);
}
