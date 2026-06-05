using Radzen;
using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Demo;
using TPS.Nexus.Kanban.Core.Interfaces;
using TPS.Nexus.Kanban.Core.Options;
using TPS.Nexus.Kanban.Demo.Mocks;
using TPS.Nexus.Kanban.Services.Hubs;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddRadzenComponents();
builder.Services.AddSignalR();

builder.Services.Configure<KanbanModuleOptions>(opt => { });

// In-memory demo implementations — no database required
builder.Services.AddSingleton<IEquipmentService,         DemoEquipmentService>();
builder.Services.AddSingleton<IAlarmService,             DemoAlarmService>();
builder.Services.AddSingleton<ILayoutService,            DemoLayoutService>();
builder.Services.AddSingleton<IMapImportService,         DemoMapImportService>();
builder.Services.AddSingleton<IDataSourceService,        DemoDataSourceService>();
builder.Services.AddSingleton<IIconUploadService,        DemoIconUploadService>();
builder.Services.AddSingleton<IIconGalleryService,       DemoIconGalleryService>();
builder.Services.AddSingleton<IFunctionPermissionService, DemoPermissionService>();
builder.Services.AddScoped<TPS.Nexus.Kanban.Web.Services.UserPrefsService>();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapHub<KanbanAlarmHub>("/hubs/kanban-alarm");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(TPS.Nexus.Kanban.Web.Components.Map.FactoryMapCanvas).Assembly);

app.Run();
