using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Core.Constants;
using TPS.Nexus.Kanban.Core.Interfaces;
using TPS.Nexus.Kanban.Core.Options;
using TPS.Nexus.Kanban.Services.Alarm;
using TPS.Nexus.Kanban.Services.DataSource;
using TPS.Nexus.Kanban.Services.Equipment;
using TPS.Nexus.Kanban.Services.Hubs;
using TPS.Nexus.Kanban.Services.Icon;
using TPS.Nexus.Kanban.Services.Layout;
using TPS.Nexus.Kanban.Services.Map;

namespace TPS.Nexus.Kanban.Services;

public class KanbanModuleRegistrar : IModuleRegistrar
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IMapImportService, MapImportService>();
        services.AddScoped<IDataSourceService, DataSourceService>();
        services.AddScoped<ILayoutService, LayoutService>();
        services.AddScoped<IEquipmentService, EquipmentService>();
        services.AddScoped<IAlarmService, AlarmService>();
        services.AddScoped<IIconUploadService, IconUploadService>();
        services.Configure<KanbanModuleOptions>(configuration.GetSection("KanbanModule"));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<KanbanAlarmHub>(KanbanRoutes.AlarmHubPath);
    }
}
