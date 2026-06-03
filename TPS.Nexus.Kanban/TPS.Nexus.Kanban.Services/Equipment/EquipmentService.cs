using Dapper;
using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Core.Interfaces;
using TPS.Nexus.Kanban.Core.Models;
using EquipmentModel = TPS.Nexus.Kanban.Core.Models.Equipment;

namespace TPS.Nexus.Kanban.Services.Equipment;

public class EquipmentService : IEquipmentService
{
    private readonly IDbConnectionFactory _db;

    public EquipmentService(IDbConnectionFactory db) => _db = db;

    public async Task<IEnumerable<EquipmentModel>> GetAllEquipmentAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<EquipmentModel>("SELECT * FROM kanban_equipment ORDER BY Name");
    }

    public async Task<EquipmentModel?> GetEquipmentAsync(int id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<EquipmentModel>(
            "SELECT * FROM kanban_equipment WHERE Id = @Id", new { Id = id });
    }

    public async Task<EquipmentModel> CreateEquipmentAsync(EquipmentModel equipment)
    {
        using var conn = _db.CreateConnection();
        equipment.Id = await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO kanban_equipment (Name, Tag, Description, IconType, IconValue)
            VALUES (@Name, @Tag, @Description, @IconType, @IconValue);
            SELECT LAST_INSERT_ID();
            """, equipment);
        return equipment;
    }

    public async Task UpdateEquipmentAsync(EquipmentModel equipment)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            """
            UPDATE kanban_equipment
            SET Name=@Name, Tag=@Tag, Description=@Description, IconType=@IconType, IconValue=@IconValue
            WHERE Id=@Id
            """, equipment);
    }

    public async Task DeleteEquipmentAsync(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM kanban_equipment WHERE Id=@Id", new { Id = id });
    }

    public async Task<IEnumerable<EquipmentWidget>> GetWidgetsByVersionAsync(int layoutVersionId)
    {
        using var conn = _db.CreateConnection();
        var widgets = (await conn.QueryAsync<EquipmentWidget>(
            "SELECT * FROM kanban_equipment_widgets WHERE LayoutVersionId=@LayoutVersionId",
            new { LayoutVersionId = layoutVersionId })).ToList();

        foreach (var w in widgets)
            w.Components = (await GetComponentsByWidgetAsync(w.Id)).ToList();

        return widgets;
    }

    public async Task<EquipmentWidget> SaveWidgetAsync(EquipmentWidget widget)
    {
        using var conn = _db.CreateConnection();
        if (widget.Id == 0)
        {
            widget.Id = await conn.ExecuteScalarAsync<int>(
                """
                INSERT INTO kanban_equipment_widgets (EquipmentId, LayoutVersionId, PositionX, PositionY, Width, Height)
                VALUES (@EquipmentId, @LayoutVersionId, @PositionX, @PositionY, @Width, @Height);
                SELECT LAST_INSERT_ID();
                """, widget);
        }
        else
        {
            await conn.ExecuteAsync(
                """
                UPDATE kanban_equipment_widgets
                SET PositionX=@PositionX, PositionY=@PositionY, Width=@Width, Height=@Height
                WHERE Id=@Id
                """, widget);
        }
        return widget;
    }

    public async Task DeleteWidgetAsync(int widgetId)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM kanban_equipment_widgets WHERE Id=@Id", new { Id = widgetId });
    }

    public async Task<IEnumerable<WidgetComponent>> GetComponentsByWidgetAsync(int widgetId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<WidgetComponent>(
            "SELECT * FROM kanban_widget_components WHERE EquipmentWidgetId=@EquipmentWidgetId ORDER BY DisplayOrder",
            new { EquipmentWidgetId = widgetId });
    }

    public async Task SaveComponentAsync(WidgetComponent component)
    {
        using var conn = _db.CreateConnection();
        if (component.Id == 0)
            await conn.ExecuteAsync(
                """
                INSERT INTO kanban_widget_components
                  (EquipmentWidgetId, ComponentType, DataSourceConfigId, Label, Unit, RefreshInterval, DisplayOrder, ConfigJson)
                VALUES
                  (@EquipmentWidgetId, @ComponentType, @DataSourceConfigId, @Label, @Unit, @RefreshInterval, @DisplayOrder, @ConfigJson)
                """, component);
        else
            await conn.ExecuteAsync(
                """
                UPDATE kanban_widget_components
                SET ComponentType=@ComponentType, DataSourceConfigId=@DataSourceConfigId,
                    Label=@Label, Unit=@Unit, RefreshInterval=@RefreshInterval,
                    DisplayOrder=@DisplayOrder, ConfigJson=@ConfigJson
                WHERE Id=@Id
                """, component);
    }

    public async Task DeleteComponentAsync(int componentId)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM kanban_widget_components WHERE Id=@Id", new { Id = componentId });
    }

    public async Task<IEnumerable<EquipmentLinkConfig>> GetLinkConfigsAsync(int equipmentId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<EquipmentLinkConfig>(
            "SELECT * FROM kanban_equipment_link_configs WHERE EquipmentId=@EquipmentId ORDER BY DisplayOrder",
            new { EquipmentId = equipmentId });
    }

    public async Task SaveLinkConfigAsync(EquipmentLinkConfig config)
    {
        using var conn = _db.CreateConnection();
        if (config.Id == 0)
            await conn.ExecuteAsync(
                """
                INSERT INTO kanban_equipment_link_configs
                  (EquipmentId, LinkType, TabLabel, UrlTemplate, DataSourceConfigId, DisplayOrder)
                VALUES
                  (@EquipmentId, @LinkType, @TabLabel, @UrlTemplate, @DataSourceConfigId, @DisplayOrder)
                """, config);
        else
            await conn.ExecuteAsync(
                """
                UPDATE kanban_equipment_link_configs
                SET LinkType=@LinkType, TabLabel=@TabLabel, UrlTemplate=@UrlTemplate,
                    DataSourceConfigId=@DataSourceConfigId, DisplayOrder=@DisplayOrder
                WHERE Id=@Id
                """, config);
    }

    public async Task DeleteLinkConfigAsync(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM kanban_equipment_link_configs WHERE Id=@Id", new { Id = id });
    }

    public async Task<IEnumerable<DataSourceConfig>> GetAllDataSourceConfigsAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<DataSourceConfig>("SELECT * FROM kanban_datasource_configs ORDER BY Name");
    }

    public async Task<DataSourceConfig> SaveDataSourceConfigAsync(DataSourceConfig config)
    {
        using var conn = _db.CreateConnection();
        if (config.Id == 0)
            config.Id = await conn.ExecuteScalarAsync<int>(
                """
                INSERT INTO kanban_datasource_configs (Name, SourceType, ConnectionString, FilePath, QueryOrPath, Parameters)
                VALUES (@Name, @SourceType, @ConnectionString, @FilePath, @QueryOrPath, @Parameters);
                SELECT LAST_INSERT_ID();
                """, config);
        else
            await conn.ExecuteAsync(
                """
                UPDATE kanban_datasource_configs
                SET Name=@Name, SourceType=@SourceType, ConnectionString=@ConnectionString,
                    FilePath=@FilePath, QueryOrPath=@QueryOrPath, Parameters=@Parameters
                WHERE Id=@Id
                """, config);
        return config;
    }

    public async Task DeleteDataSourceConfigAsync(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM kanban_datasource_configs WHERE Id=@Id", new { Id = id });
    }
}
