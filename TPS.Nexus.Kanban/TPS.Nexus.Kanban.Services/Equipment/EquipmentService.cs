using TPS.Nexus.Kanban.Core.Interfaces;
using TPS.Nexus.Kanban.Core.Models;
using CoreModels = TPS.Nexus.Kanban.Core.Models;
namespace TPS.Nexus.Kanban.Services.Equipment;
public class EquipmentService : IEquipmentService
{
    public Task<IEnumerable<CoreModels.Equipment>> GetAllEquipmentAsync() => throw new NotImplementedException();
    public Task<CoreModels.Equipment?> GetEquipmentAsync(int id) => throw new NotImplementedException();
    public Task<CoreModels.Equipment> CreateEquipmentAsync(CoreModels.Equipment equipment) => throw new NotImplementedException();
    public Task UpdateEquipmentAsync(CoreModels.Equipment equipment) => throw new NotImplementedException();
    public Task DeleteEquipmentAsync(int id) => throw new NotImplementedException();
    public Task<IEnumerable<EquipmentWidget>> GetWidgetsByVersionAsync(int layoutVersionId) => throw new NotImplementedException();
    public Task<EquipmentWidget> SaveWidgetAsync(EquipmentWidget widget) => throw new NotImplementedException();
    public Task DeleteWidgetAsync(int widgetId) => throw new NotImplementedException();
    public Task<IEnumerable<WidgetComponent>> GetComponentsByWidgetAsync(int widgetId) => throw new NotImplementedException();
    public Task SaveComponentAsync(WidgetComponent component) => throw new NotImplementedException();
    public Task DeleteComponentAsync(int componentId) => throw new NotImplementedException();
    public Task<IEnumerable<EquipmentLinkConfig>> GetLinkConfigsAsync(int equipmentId) => throw new NotImplementedException();
    public Task SaveLinkConfigAsync(EquipmentLinkConfig config) => throw new NotImplementedException();
    public Task DeleteLinkConfigAsync(int id) => throw new NotImplementedException();
    public Task<IEnumerable<DataSourceConfig>> GetAllDataSourceConfigsAsync() => throw new NotImplementedException();
    public Task<DataSourceConfig> SaveDataSourceConfigAsync(DataSourceConfig config) => throw new NotImplementedException();
    public Task DeleteDataSourceConfigAsync(int id) => throw new NotImplementedException();
}
