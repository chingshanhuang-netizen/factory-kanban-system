using Radzen;
using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Demo;
using TPS.Nexus.Kanban.Core.Interfaces;
using TPS.Nexus.Kanban.Core.Options;
using TPS.Nexus.Kanban.Demo.Infrastructure;
using TPS.Nexus.Kanban.Demo.Mocks;
using TPS.Nexus.Kanban.Services.Alarm;
using TPS.Nexus.Kanban.Services.Equipment;
using TPS.Nexus.Kanban.Services.Hubs;
using TPS.Nexus.Kanban.Services.Layout;
using TPS.Nexus.Kanban.Services.Map;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();
builder.WebHost.UseUrls("http://0.0.0.0:5000");

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddRadzenComponents();
builder.Services.AddSignalR();

builder.Services.Configure<KanbanModuleOptions>(opt => { });

// ── Infrastructure ────────────────────────────────────────────
var connStr = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is missing in appsettings.json.");

builder.Services.AddSingleton<IDbConnectionFactory>(new MySqlConnectionFactory(connStr));

builder.Services.AddSingleton<IWebHostEnvironmentAccessor>(sp =>
    new WebHostEnvironmentAccessor(
        sp.GetRequiredService<IWebHostEnvironment>().WebRootPath
        ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")));

// ── Real DB services (Scoped — one DB connection per Blazor circuit) ──
builder.Services.AddScoped<IEquipmentService,  EquipmentService>();
builder.Services.AddScoped<ILayoutService,     LayoutService>();
builder.Services.AddScoped<IMapImportService,  MapImportService>();
builder.Services.AddScoped<IAlarmService,      AlarmService>();

// ── Demo-only stubs (no real counterpart needed for demo) ─────
builder.Services.AddSingleton<IDataSourceService,         DemoDataSourceService>();
builder.Services.AddSingleton<IIconUploadService,         DemoIconUploadService>();
builder.Services.AddSingleton<IIconGalleryService,        DemoIconGalleryService>();
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
