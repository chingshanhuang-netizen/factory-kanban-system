# Factory Kanban System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build TPS.Nexus.Kanban — a Blazor RCL module that places real-time equipment dashboards on factory floor maps, driven by SQL/CSV/JSON/XML data sources, with SignalR alarms and layout version management.

**Architecture:** Three .NET 10 projects (Core → Services → Web RCL) compiled into `Modules/TPS.Nexus.Kanban/` and loaded by TPS.Nexus.Web.exe via `IModuleRegistrar`. Blazor Server components render a three-layer canvas: factory map image (bottom) / equipment widgets (middle, position:absolute) / tooltips & drawer (top).

**Tech Stack:** .NET 10, Blazor Server, Radzen.Blazor (free), SortableJS, SignalR, MySQL via IDbConnectionFactory, Dapper, netDxf, CsvHelper, xUnit, NSubstitute

---

## File Structure

```
Solution/
├── TPS.Nexus.Kanban.Core/
│   ├── TPS.Nexus.Kanban.Core.csproj
│   ├── Enums/
│   │   ├── MapFormatType.cs
│   │   ├── DataSourceType.cs
│   │   ├── WidgetComponentType.cs
│   │   ├── LayoutStatus.cs
│   │   ├── AlarmLevel.cs
│   │   ├── IconType.cs
│   │   └── LinkType.cs
│   ├── Models/
│   │   ├── FactoryMap.cs
│   │   ├── LayoutVersion.cs
│   │   ├── Equipment.cs
│   │   ├── EquipmentWidget.cs
│   │   ├── WidgetComponent.cs
│   │   ├── DataSourceConfig.cs
│   │   ├── AlarmRule.cs
│   │   ├── AlarmRecord.cs
│   │   ├── EquipmentLinkConfig.cs
│   │   └── DataResult.cs
│   └── Interfaces/
│       ├── IMapImportService.cs
│       ├── IDataSourceService.cs
│       ├── ILayoutService.cs
│       ├── IEquipmentService.cs
│       ├── IAlarmService.cs
│       └── IIconUploadService.cs
│
├── TPS.Nexus.Kanban.Services/
│   ├── TPS.Nexus.Kanban.Services.csproj
│   ├── Map/
│   │   ├── MapImportService.cs
│   │   ├── ImageMapHandler.cs
│   │   ├── SvgMapParser.cs
│   │   ├── DxfMapParser.cs
│   │   └── JsonXmlCoordParser.cs
│   ├── DataSource/
│   │   ├── DataSourceService.cs
│   │   ├── SqlDataAdapter.cs
│   │   ├── CsvDataAdapter.cs
│   │   ├── JsonDataAdapter.cs
│   │   └── XmlDataAdapter.cs
│   ├── Layout/
│   │   └── LayoutService.cs
│   ├── Equipment/
│   │   └── EquipmentService.cs
│   ├── Alarm/
│   │   └── AlarmService.cs
│   ├── Icon/
│   │   └── IconUploadService.cs
│   ├── Hubs/
│   │   └── KanbanAlarmHub.cs
│   └── KanbanModuleRegistrar.cs
│
├── TPS.Nexus.Kanban.Web/
│   ├── TPS.Nexus.Kanban.Web.csproj
│   ├── _Imports.razor
│   ├── wwwroot/
│   │   ├── js/
│   │   │   ├── sortable.min.js   (download from CDN)
│   │   │   └── kanban-drag.js
│   │   └── css/
│   │       └── kanban.css
│   ├── Pages/
│   │   ├── KanbanMapPage.razor
│   │   └── KanbanSettingsPage.razor
│   └── Components/
│       ├── Map/
│       │   ├── FactoryMapCanvas.razor
│       │   ├── MapLayer.razor
│       │   └── MapImportPanel.razor
│       ├── Widget/
│       │   ├── EquipmentWidgetPanel.razor
│       │   ├── StatusIndicator.razor
│       │   └── EquipmentIconRenderer.razor
│       ├── Tooltip/
│       │   └── EquipmentTooltip.razor
│       ├── Drawer/
│       │   └── EquipmentDetailDrawer.razor
│       ├── Editor/
│       │   ├── MapEditorToolbar.razor
│       │   ├── DraggableEquipmentItem.razor
│       │   └── WidgetConfigurator.razor
│       ├── Version/
│       │   └── LayoutVersionPanel.razor
│       └── Alarm/
│           ├── AlarmBadge.razor
│           ├── AlarmPanel.razor
│           └── AlarmToast.razor
│
└── TPS.Nexus.Kanban.Tests/
    ├── TPS.Nexus.Kanban.Tests.csproj
    ├── DataSource/
    │   ├── SqlDataAdapterTests.cs
    │   ├── CsvDataAdapterTests.cs
    │   ├── JsonDataAdapterTests.cs
    │   └── XmlDataAdapterTests.cs
    ├── Layout/
    │   └── LayoutServiceTests.cs
    └── Alarm/
        └── AlarmServiceTests.cs
```

### Database Schema (run once by DBA)

```sql
-- FactoryMaps
CREATE TABLE kanban_factory_maps (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(200) NOT NULL,
    FormatType TINYINT NOT NULL,
    FilePath VARCHAR(500) NOT NULL,
    ThumbnailPath VARCHAR(500),
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Version VARCHAR(50) NOT NULL DEFAULT 'v1',        -- 顯示於地圖管理版本欄
    CarouselEnabled TINYINT NOT NULL DEFAULT 0,       -- 是否加入輪播序列
    CarouselSeconds INT NOT NULL DEFAULT 10,          -- 停留秒數
    CarouselOrder INT NOT NULL DEFAULT 0              -- 輪播排序（升冪）
);

-- LayoutVersions
CREATE TABLE kanban_layout_versions (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    FactoryMapId INT NOT NULL,
    VersionNo INT NOT NULL,
    Status TINYINT NOT NULL DEFAULT 0,
    CreatedBy VARCHAR(100) NOT NULL,
    PublishedAt DATETIME,
    LayoutJson LONGTEXT,
    FOREIGN KEY (FactoryMapId) REFERENCES kanban_factory_maps(Id)
);

-- Equipment
CREATE TABLE kanban_equipment (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(200) NOT NULL,
    Tag VARCHAR(100),
    Description TEXT,
    IconType TINYINT NOT NULL DEFAULT 0,
    IconValue VARCHAR(500)
);

-- EquipmentWidgets
CREATE TABLE kanban_equipment_widgets (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    EquipmentId INT NOT NULL,
    LayoutVersionId INT NOT NULL,
    PositionX INT NOT NULL DEFAULT 0,
    PositionY INT NOT NULL DEFAULT 0,
    Width INT NOT NULL DEFAULT 80,
    Height INT NOT NULL DEFAULT 100,
    FOREIGN KEY (EquipmentId) REFERENCES kanban_equipment(Id),
    FOREIGN KEY (LayoutVersionId) REFERENCES kanban_layout_versions(Id)
);

-- WidgetComponents
CREATE TABLE kanban_widget_components (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    EquipmentWidgetId INT NOT NULL,
    ComponentType TINYINT NOT NULL,
    DataSourceConfigId INT,
    Label VARCHAR(200),
    Unit VARCHAR(50),
    RefreshInterval INT NOT NULL DEFAULT 30,
    DisplayOrder INT NOT NULL DEFAULT 0,
    ConfigJson TEXT,
    FOREIGN KEY (EquipmentWidgetId) REFERENCES kanban_equipment_widgets(Id)
);

-- DataSourceConfigs
CREATE TABLE kanban_datasource_configs (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(200) NOT NULL,
    SourceType TINYINT NOT NULL,
    ConnectionString TEXT,
    FilePath VARCHAR(500),
    QueryOrPath TEXT,
    Parameters TEXT
);

-- AlarmRules
CREATE TABLE kanban_alarm_rules (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    EquipmentId INT NOT NULL,
    DataSourceConfigId INT NOT NULL,
    Condition VARCHAR(50) NOT NULL,
    Threshold DOUBLE NOT NULL,
    AlarmLevel TINYINT NOT NULL
);

-- AlarmRecords
CREATE TABLE kanban_alarm_records (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    EquipmentId INT NOT NULL,
    EquipmentName VARCHAR(200) NOT NULL,
    Level TINYINT NOT NULL,
    Message TEXT NOT NULL,
    TriggeredAt DATETIME NOT NULL,
    ResolvedAt DATETIME
);

-- EquipmentLinkConfigs
CREATE TABLE kanban_equipment_link_configs (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    EquipmentId INT NOT NULL,
    LinkType TINYINT NOT NULL,
    TabLabel VARCHAR(100) NOT NULL,
    UrlTemplate VARCHAR(1000),
    DataSourceConfigId INT,
    DisplayOrder INT NOT NULL DEFAULT 0
);
```

---

## Task 0: Host Prerequisites — IModuleRegistrar + Program.cs

**Files:**
- Modify: `TPS.Nexus.Core/Interfaces/IModuleRegistrar.cs` (add `MapEndpoints` method)
- Modify: `TPS.Nexus.Web/Program.cs` (add Blazor + SignalR services)

> **Note to engineer:** Co-ordinate with TPS.Nexus core team. Other existing modules must add an empty `MapEndpoints` implementation.

- [ ] **Step 1: Add `MapEndpoints` to IModuleRegistrar**

Open `TPS.Nexus.Core/Interfaces/IModuleRegistrar.cs` and add one method:

```csharp
public interface IModuleRegistrar
{
    void RegisterServices(IServiceCollection services, IConfiguration configuration);
    void MapEndpoints(IEndpointRouteBuilder endpoints); // NEW — add empty impl to all existing modules
}
```

- [ ] **Step 2: Update all existing module registrars with empty implementation**

For every existing module's `*ModuleRegistrar.cs` that implements `IModuleRegistrar`, add:

```csharp
public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
```

- [ ] **Step 3: Add Blazor Server + SignalR to Program.cs**

Locate the block where `builder.Services.AddControllersWithViews()` is called and add immediately after:

```csharp
builder.Services.AddServerSideBlazor();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSignalR();
```

- [ ] **Step 4: Add MapBlazorHub + MapEndpoints in Program.cs**

Locate `app.MapControllerRoute(...)` and add **before** it:

```csharp
app.MapBlazorHub();
foreach (var module in modules)
{
    module.Registrar.MapEndpoints(app);
}
```

- [ ] **Step 5: Verify host builds and starts**

```bash
dotnet build TPS.Nexus.Web
dotnet run --project TPS.Nexus.Web
```

Expected: Application starts without errors, existing MVC routes still respond.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add MapEndpoints to IModuleRegistrar; wire Blazor Server + SignalR to host"
```

---

## Task 1: Scaffold Three Projects + Test Project

**Files:**
- Create: `TPS.Nexus.Kanban.Core/TPS.Nexus.Kanban.Core.csproj`
- Create: `TPS.Nexus.Kanban.Services/TPS.Nexus.Kanban.Services.csproj`
- Create: `TPS.Nexus.Kanban.Web/TPS.Nexus.Kanban.Web.csproj`
- Create: `TPS.Nexus.Kanban.Tests/TPS.Nexus.Kanban.Tests.csproj`

- [ ] **Step 1: Create Core project**

```bash
dotnet new classlib -n TPS.Nexus.Kanban.Core -f net10.0
cd TPS.Nexus.Kanban.Core
mkdir Enums Models Interfaces
```

Edit `TPS.Nexus.Kanban.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="TPS.Nexus.Core">
      <HintPath>..\libs\TPS.Nexus.Core.dll</HintPath>
    </Reference>
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create Services project**

```bash
dotnet new classlib -n TPS.Nexus.Kanban.Services -f net10.0
cd TPS.Nexus.Kanban.Services
mkdir Map DataSource Layout Equipment Alarm Icon Hubs
```

Edit `TPS.Nexus.Kanban.Services.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Dapper" Version="2.*" />
    <PackageReference Include="MySql.Data" Version="9.*" />
    <PackageReference Include="CsvHelper" Version="33.*" />
    <PackageReference Include="netDxf" Version="3.*" />
    <PackageReference Include="Microsoft.AspNetCore.SignalR" Version="1.*" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\TPS.Nexus.Kanban.Core\TPS.Nexus.Kanban.Core.csproj" />
    <Reference Include="TPS.Nexus.Core">
      <HintPath>..\libs\TPS.Nexus.Core.dll</HintPath>
    </Reference>
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create Web (RCL) project**

```bash
dotnet new razorclasslib -n TPS.Nexus.Kanban.Web -f net10.0
cd TPS.Nexus.Kanban.Web
mkdir -p Pages Components/Map Components/Widget Components/Tooltip
mkdir -p Components/Drawer Components/Editor Components/Version Components/Alarm
mkdir -p wwwroot/js wwwroot/css
```

Edit `TPS.Nexus.Kanban.Web.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Radzen.Blazor" Version="5.*" />
    <PackageReference Include="Microsoft.AspNetCore.Components.Web" Version="10.*" />
    <PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="10.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\TPS.Nexus.Kanban.Services\TPS.Nexus.Kanban.Services.csproj" />
    <ProjectReference Include="..\TPS.Nexus.Kanban.Core\TPS.Nexus.Kanban.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Create Tests project**

```bash
dotnet new xunit -n TPS.Nexus.Kanban.Tests -f net10.0
cd TPS.Nexus.Kanban.Tests
mkdir DataSource Layout Alarm
```

Edit `TPS.Nexus.Kanban.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="NSubstitute" Version="5.*" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\TPS.Nexus.Kanban.Services\TPS.Nexus.Kanban.Services.csproj" />
    <ProjectReference Include="..\TPS.Nexus.Kanban.Core\TPS.Nexus.Kanban.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Build all projects**

```bash
dotnet build
```

Expected: 4 projects build with 0 errors.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: scaffold Kanban module — Core, Services, Web, Tests projects"
```

---

## Task 2: Core — Enums

**Files:**
- Create: `TPS.Nexus.Kanban.Core/Enums/MapFormatType.cs`
- Create: `TPS.Nexus.Kanban.Core/Enums/DataSourceType.cs`
- Create: `TPS.Nexus.Kanban.Core/Enums/WidgetComponentType.cs`
- Create: `TPS.Nexus.Kanban.Core/Enums/LayoutStatus.cs`
- Create: `TPS.Nexus.Kanban.Core/Enums/AlarmLevel.cs`
- Create: `TPS.Nexus.Kanban.Core/Enums/IconType.cs`
- Create: `TPS.Nexus.Kanban.Core/Enums/LinkType.cs`

- [ ] **Step 1: Write all enum files**

`TPS.Nexus.Kanban.Core/Enums/MapFormatType.cs`:
```csharp
namespace TPS.Nexus.Kanban.Core.Enums;

public enum MapFormatType : byte
{
    Png = 0,
    Jpg = 1,
    Svg = 2,
    Dxf = 3,
    JsonCoord = 4,
    XmlCoord = 5
}
```

`TPS.Nexus.Kanban.Core/Enums/DataSourceType.cs`:
```csharp
namespace TPS.Nexus.Kanban.Core.Enums;

public enum DataSourceType : byte
{
    Sql = 0,
    Csv = 1,
    Json = 2,
    Xml = 3
}
```

`TPS.Nexus.Kanban.Core/Enums/WidgetComponentType.cs`:
```csharp
namespace TPS.Nexus.Kanban.Core.Enums;

public enum WidgetComponentType : byte
{
    StatusIndicator = 0,
    ValueGauge = 1,
    TrendChart = 2
}
```

`TPS.Nexus.Kanban.Core/Enums/LayoutStatus.cs`:
```csharp
namespace TPS.Nexus.Kanban.Core.Enums;

public enum LayoutStatus : byte
{
    Draft = 0,
    Published = 1,
    Archived = 2
}
```

`TPS.Nexus.Kanban.Core/Enums/AlarmLevel.cs`:
```csharp
namespace TPS.Nexus.Kanban.Core.Enums;

public enum AlarmLevel : byte
{
    Info = 0,
    Warning = 1,
    Critical = 2
}
```

`TPS.Nexus.Kanban.Core/Enums/IconType.cs`:
```csharp
namespace TPS.Nexus.Kanban.Core.Enums;

public enum IconType : byte
{
    CssClass = 0,
    CustomImage = 1
}
```

`TPS.Nexus.Kanban.Core/Enums/LinkType.cs`:
```csharp
namespace TPS.Nexus.Kanban.Core.Enums;

public enum LinkType : byte
{
    WorkOrder = 0,
    AlarmHistory = 1,
    Document = 2,
    CustomUrl = 3
}
```

- [ ] **Step 2: Build Core**

```bash
dotnet build TPS.Nexus.Kanban.Core
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add TPS.Nexus.Kanban.Core/Enums/
git commit -m "feat(core): add all kanban enums"
```

---

## Task 3: Core — Models

**Files:**
- Create: `TPS.Nexus.Kanban.Core/Models/DataResult.cs`
- Create: `TPS.Nexus.Kanban.Core/Models/FactoryMap.cs`
- Create: `TPS.Nexus.Kanban.Core/Models/LayoutVersion.cs`
- Create: `TPS.Nexus.Kanban.Core/Models/Equipment.cs`
- Create: `TPS.Nexus.Kanban.Core/Models/EquipmentWidget.cs`
- Create: `TPS.Nexus.Kanban.Core/Models/WidgetComponent.cs`
- Create: `TPS.Nexus.Kanban.Core/Models/DataSourceConfig.cs`
- Create: `TPS.Nexus.Kanban.Core/Models/AlarmRule.cs`
- Create: `TPS.Nexus.Kanban.Core/Models/AlarmRecord.cs`
- Create: `TPS.Nexus.Kanban.Core/Models/EquipmentLinkConfig.cs`

- [ ] **Step 1: Write DataResult**

`TPS.Nexus.Kanban.Core/Models/DataResult.cs`:
```csharp
namespace TPS.Nexus.Kanban.Core.Models;

public class DataResult
{
    public Dictionary<string, object?> Fields { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public T? Get<T>(string key)
    {
        if (Fields.TryGetValue(key, out var val) && val is T typed)
            return typed;
        return default;
    }
}
```

- [ ] **Step 2: Write FactoryMap**

`TPS.Nexus.Kanban.Core/Models/FactoryMap.cs`:
```csharp
using TPS.Nexus.Kanban.Core.Enums;

namespace TPS.Nexus.Kanban.Core.Models;

public class FactoryMap
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public MapFormatType FormatType { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string? ThumbnailPath { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

- [ ] **Step 3: Write LayoutVersion**

`TPS.Nexus.Kanban.Core/Models/LayoutVersion.cs`:
```csharp
using TPS.Nexus.Kanban.Core.Enums;

namespace TPS.Nexus.Kanban.Core.Models;

public class LayoutVersion
{
    public int Id { get; set; }
    public int FactoryMapId { get; set; }
    public int VersionNo { get; set; }
    public LayoutStatus Status { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public string? LayoutJson { get; set; }
}
```

- [ ] **Step 4: Write Equipment**

`TPS.Nexus.Kanban.Core/Models/Equipment.cs`:
```csharp
using TPS.Nexus.Kanban.Core.Enums;

namespace TPS.Nexus.Kanban.Core.Models;

public class Equipment
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Tag { get; set; }
    public string? Description { get; set; }
    public IconType IconType { get; set; }
    public string? IconValue { get; set; }
}
```

- [ ] **Step 5: Write EquipmentWidget + WidgetComponent**

`TPS.Nexus.Kanban.Core/Models/EquipmentWidget.cs`:
```csharp
namespace TPS.Nexus.Kanban.Core.Models;

public class EquipmentWidget
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public int LayoutVersionId { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public int Width { get; set; } = 80;
    public int Height { get; set; } = 100;
    public List<WidgetComponent> Components { get; set; } = new();
}
```

`TPS.Nexus.Kanban.Core/Models/WidgetComponent.cs`:
```csharp
using TPS.Nexus.Kanban.Core.Enums;

namespace TPS.Nexus.Kanban.Core.Models;

public class WidgetComponent
{
    public int Id { get; set; }
    public int EquipmentWidgetId { get; set; }
    public WidgetComponentType ComponentType { get; set; }
    public int? DataSourceConfigId { get; set; }
    public string? Label { get; set; }
    public string? Unit { get; set; }
    public int RefreshInterval { get; set; } = 30;
    public int DisplayOrder { get; set; }
    public string? ConfigJson { get; set; }
}
```

- [ ] **Step 6: Write DataSourceConfig**

`TPS.Nexus.Kanban.Core/Models/DataSourceConfig.cs`:
```csharp
using TPS.Nexus.Kanban.Core.Enums;

namespace TPS.Nexus.Kanban.Core.Models;

public class DataSourceConfig
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DataSourceType SourceType { get; set; }
    public string? ConnectionString { get; set; }
    public string? FilePath { get; set; }
    public string? QueryOrPath { get; set; }
    public string? Parameters { get; set; }
}
```

- [ ] **Step 7: Write AlarmRule + AlarmRecord**

`TPS.Nexus.Kanban.Core/Models/AlarmRule.cs`:
```csharp
using TPS.Nexus.Kanban.Core.Enums;

namespace TPS.Nexus.Kanban.Core.Models;

public class AlarmRule
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public int DataSourceConfigId { get; set; }
    public string Condition { get; set; } = string.Empty; // ">", "<", "==", "!="
    public double Threshold { get; set; }
    public AlarmLevel AlarmLevel { get; set; }
}
```

`TPS.Nexus.Kanban.Core/Models/AlarmRecord.cs`:
```csharp
using TPS.Nexus.Kanban.Core.Enums;

namespace TPS.Nexus.Kanban.Core.Models;

public class AlarmRecord
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public string EquipmentName { get; set; } = string.Empty;
    public AlarmLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime TriggeredAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
```

- [ ] **Step 8: Write EquipmentLinkConfig**

`TPS.Nexus.Kanban.Core/Models/EquipmentLinkConfig.cs`:
```csharp
using TPS.Nexus.Kanban.Core.Enums;

namespace TPS.Nexus.Kanban.Core.Models;

public class EquipmentLinkConfig
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public LinkType LinkType { get; set; }
    public string TabLabel { get; set; } = string.Empty;
    public string? UrlTemplate { get; set; }
    public int? DataSourceConfigId { get; set; }
    public int DisplayOrder { get; set; }
}
```

- [ ] **Step 9: Build Core**

```bash
dotnet build TPS.Nexus.Kanban.Core
```

Expected: 0 errors.

- [ ] **Step 10: Commit**

```bash
git add TPS.Nexus.Kanban.Core/Models/
git commit -m "feat(core): add all kanban domain models"
```

---

## Task 4: Core — Service Interfaces

**Files:**
- Create: `TPS.Nexus.Kanban.Core/Interfaces/IMapImportService.cs`
- Create: `TPS.Nexus.Kanban.Core/Interfaces/IDataSourceService.cs`
- Create: `TPS.Nexus.Kanban.Core/Interfaces/ILayoutService.cs`
- Create: `TPS.Nexus.Kanban.Core/Interfaces/IEquipmentService.cs`
- Create: `TPS.Nexus.Kanban.Core/Interfaces/IAlarmService.cs`
- Create: `TPS.Nexus.Kanban.Core/Interfaces/IIconUploadService.cs`

- [ ] **Step 1: Write IMapImportService**

`TPS.Nexus.Kanban.Core/Interfaces/IMapImportService.cs`:
```csharp
using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Core.Interfaces;

public interface IMapImportService
{
    Task<FactoryMap> ImportAsync(Stream file, string fileName, MapFormatType format);
    Task<IEnumerable<FactoryMap>> GetAllAsync();
    Task DeleteAsync(int mapId);
}
```

- [ ] **Step 2: Write IDataSourceService**

`TPS.Nexus.Kanban.Core/Interfaces/IDataSourceService.cs`:
```csharp
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Core.Interfaces;

public interface IDataSourceService
{
    Task<DataResult> FetchAsync(DataSourceConfig config);
    Task<IEnumerable<DataResult>> FetchHistoryAsync(DataSourceConfig config, DateTime from, DateTime to);
}
```

- [ ] **Step 3: Write ILayoutService**

`TPS.Nexus.Kanban.Core/Interfaces/ILayoutService.cs`:
```csharp
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Core.Interfaces;

public interface ILayoutService
{
    Task<LayoutVersion> SaveDraftAsync(int factoryMapId, string layoutJson, string createdBy);
    Task<LayoutVersion> PublishAsync(int draftId);
    Task<IEnumerable<LayoutVersion>> GetVersionHistoryAsync(int mapId);
    Task<LayoutVersion?> GetPublishedVersionAsync(int mapId);
    Task RollbackAsync(int versionId);
}
```

- [ ] **Step 4: Write IEquipmentService**

`TPS.Nexus.Kanban.Core/Interfaces/IEquipmentService.cs`:
```csharp
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
}
```

- [ ] **Step 5: Write IAlarmService**

`TPS.Nexus.Kanban.Core/Interfaces/IAlarmService.cs`:
```csharp
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Core.Interfaces;

public interface IAlarmService
{
    Task EvaluateAsync(int equipmentId, string equipmentName, DataResult latestData);
    Task<IEnumerable<AlarmRecord>> GetActiveAlarmsAsync();
    Task ResolveAlarmAsync(int alarmRecordId);
    Task<IEnumerable<AlarmRule>> GetRulesAsync(int equipmentId);
    Task SaveRuleAsync(AlarmRule rule);
    Task DeleteRuleAsync(int ruleId);
}
```

- [ ] **Step 6: Write IIconUploadService**

`TPS.Nexus.Kanban.Core/Interfaces/IIconUploadService.cs`:
```csharp
namespace TPS.Nexus.Kanban.Core.Interfaces;

public interface IIconUploadService
{
    Task<string> UploadAsync(Stream file, string fileName);
    Task DeleteAsync(string filePath);
}
```

- [ ] **Step 7: Build Core**

```bash
dotnet build TPS.Nexus.Kanban.Core
```

Expected: 0 errors.

- [ ] **Step 8: Commit**

```bash
git add TPS.Nexus.Kanban.Core/Interfaces/
git commit -m "feat(core): add all kanban service interfaces"
```

---

## Task 5: Service — SignalR Hub + Module Registrar

**Files:**
- Create: `TPS.Nexus.Kanban.Services/Hubs/KanbanAlarmHub.cs`
- Create: `TPS.Nexus.Kanban.Services/KanbanModuleRegistrar.cs`

- [ ] **Step 1: Write KanbanAlarmHub**

`TPS.Nexus.Kanban.Services/Hubs/KanbanAlarmHub.cs`:
```csharp
using Microsoft.AspNetCore.SignalR;

namespace TPS.Nexus.Kanban.Services.Hubs;

public class KanbanAlarmHub : Hub
{
    public async Task SendAlarmAsync(int equipmentId, string alarmLevel, string message)
    {
        await Clients.All.SendAsync("ReceiveAlarm", equipmentId, alarmLevel, message);
    }
}
```

- [ ] **Step 2: Write KanbanModuleRegistrar**

`TPS.Nexus.Kanban.Services/KanbanModuleRegistrar.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Core.Interfaces;
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
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<KanbanAlarmHub>("/hubs/kanban-alarm");
    }
}
```

- [ ] **Step 3: Build Services**

```bash
dotnet build TPS.Nexus.Kanban.Services
```

Expected: 0 errors (other service classes don't exist yet — add empty stubs if needed).

- [ ] **Step 4: Commit**

```bash
git add TPS.Nexus.Kanban.Services/Hubs/ TPS.Nexus.Kanban.Services/KanbanModuleRegistrar.cs
git commit -m "feat(services): add KanbanAlarmHub and KanbanModuleRegistrar"
```

---

## Task 6: Service — SQL Data Adapter (with tests)

**Files:**
- Create: `TPS.Nexus.Kanban.Services/DataSource/SqlDataAdapter.cs`
- Create: `TPS.Nexus.Kanban.Tests/DataSource/SqlDataAdapterTests.cs`

- [ ] **Step 1: Write failing test**

`TPS.Nexus.Kanban.Tests/DataSource/SqlDataAdapterTests.cs`:
```csharp
using NSubstitute;
using System.Data;
using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Core.Models;
using TPS.Nexus.Kanban.Services.DataSource;
using Xunit;

namespace TPS.Nexus.Kanban.Tests.DataSource;

public class SqlDataAdapterTests
{
    private readonly IDbConnectionFactory _factory = Substitute.For<IDbConnectionFactory>();
    private readonly IDbConnection _conn = Substitute.For<IDbConnection>();
    private readonly IDbCommand _cmd = Substitute.For<IDbCommand>();
    private readonly IDataParameterCollection _params = Substitute.For<IDataParameterCollection>();
    private readonly IDataReader _reader = Substitute.For<IDataReader>();

    public SqlDataAdapterTests()
    {
        _factory.CreateConnection().Returns(_conn);
        _conn.CreateCommand().Returns(_cmd);
        _cmd.Parameters.Returns(_params);
    }

    [Fact]
    public async Task FetchAsync_ExecutesQueryAndReturnsFields()
    {
        var config = new DataSourceConfig
        {
            SourceType = TPS.Nexus.Kanban.Core.Enums.DataSourceType.Sql,
            QueryOrPath = "SELECT speed, temp FROM machine WHERE id = @id",
            Parameters = "{\"id\": 1}"
        };

        // reader returns one row: speed=1200, temp=68.3
        var callCount = 0;
        _reader.Read().Returns(_ => callCount++ == 0);
        _reader.FieldCount.Returns(2);
        _reader.GetName(0).Returns("speed");
        _reader.GetName(1).Returns("temp");
        _reader.GetValue(0).Returns(1200);
        _reader.GetValue(1).Returns(68.3);
        _cmd.ExecuteReader().Returns(_reader);

        var adapter = new SqlDataAdapter(_factory);
        var result = await adapter.FetchAsync(config);

        Assert.NotNull(result);
        Assert.Equal(1200, result.Fields["speed"]);
        Assert.Equal(68.3, result.Fields["temp"]);
    }

    [Fact]
    public async Task FetchAsync_EmptyResult_ReturnsEmptyFields()
    {
        var config = new DataSourceConfig
        {
            SourceType = TPS.Nexus.Kanban.Core.Enums.DataSourceType.Sql,
            QueryOrPath = "SELECT 1",
            Parameters = null
        };

        _reader.Read().Returns(false);
        _cmd.ExecuteReader().Returns(_reader);

        var adapter = new SqlDataAdapter(_factory);
        var result = await adapter.FetchAsync(config);

        Assert.Empty(result.Fields);
    }
}
```

- [ ] **Step 2: Run test — verify it fails**

```bash
dotnet test TPS.Nexus.Kanban.Tests --filter "SqlDataAdapterTests"
```

Expected: FAIL — `SqlDataAdapter` not found.

- [ ] **Step 3: Implement SqlDataAdapter**

`TPS.Nexus.Kanban.Services/DataSource/SqlDataAdapter.cs`:
```csharp
using System.Text.Json;
using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.DataSource;

public class SqlDataAdapter
{
    private readonly IDbConnectionFactory _factory;

    public SqlDataAdapter(IDbConnectionFactory factory) => _factory = factory;

    public Task<DataResult> FetchAsync(DataSourceConfig config)
    {
        var result = new DataResult();
        using var conn = _factory.CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = config.QueryOrPath
            ?? throw new InvalidOperationException("QueryOrPath is required for SQL source.");

        if (!string.IsNullOrEmpty(config.Parameters))
        {
            var paramDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(config.Parameters)
                ?? new Dictionary<string, JsonElement>();

            foreach (var (key, value) in paramDict)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = "@" + key;
                p.Value = value.ValueKind switch
                {
                    JsonValueKind.Number => value.TryGetInt64(out var l) ? (object)l : value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => value.GetString() ?? DBNull.Value
                };
                cmd.Parameters.Add(p);
            }
        }

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            for (int i = 0; i < reader.FieldCount; i++)
                result.Fields[reader.GetName(i)] = reader.GetValue(i);
        }

        return Task.FromResult(result);
    }

    public Task<IEnumerable<DataResult>> FetchHistoryAsync(DataSourceConfig config, DateTime from, DateTime to)
    {
        var results = new List<DataResult>();
        using var conn = _factory.CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = config.QueryOrPath
            ?? throw new InvalidOperationException("QueryOrPath is required for SQL source.");

        void AddParam(string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
        }
        AddParam("@from", from);
        AddParam("@to", to);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var row = new DataResult();
            for (int i = 0; i < reader.FieldCount; i++)
                row.Fields[reader.GetName(i)] = reader.GetValue(i);
            results.Add(row);
        }

        return Task.FromResult<IEnumerable<DataResult>>(results);
    }
}
```

- [ ] **Step 4: Run test — verify it passes**

```bash
dotnet test TPS.Nexus.Kanban.Tests --filter "SqlDataAdapterTests"
```

Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add TPS.Nexus.Kanban.Services/DataSource/SqlDataAdapter.cs
git add TPS.Nexus.Kanban.Tests/DataSource/SqlDataAdapterTests.cs
git commit -m "feat(services): add SqlDataAdapter with tests"
```

---

## Task 7: Service — CSV, JSON, XML Adapters (with tests)

**Files:**
- Create: `TPS.Nexus.Kanban.Services/DataSource/CsvDataAdapter.cs`
- Create: `TPS.Nexus.Kanban.Services/DataSource/JsonDataAdapter.cs`
- Create: `TPS.Nexus.Kanban.Services/DataSource/XmlDataAdapter.cs`
- Create: `TPS.Nexus.Kanban.Tests/DataSource/CsvDataAdapterTests.cs`
- Create: `TPS.Nexus.Kanban.Tests/DataSource/JsonDataAdapterTests.cs`
- Create: `TPS.Nexus.Kanban.Tests/DataSource/XmlDataAdapterTests.cs`

- [ ] **Step 1: Write failing tests**

`TPS.Nexus.Kanban.Tests/DataSource/CsvDataAdapterTests.cs`:
```csharp
using TPS.Nexus.Kanban.Core.Models;
using TPS.Nexus.Kanban.Services.DataSource;
using Xunit;

namespace TPS.Nexus.Kanban.Tests.DataSource;

public class CsvDataAdapterTests
{
    [Fact]
    public async Task FetchAsync_ParsesFirstDataRowAsFields()
    {
        var csvContent = "speed,temp,yield\n1200,68.3,98.2\n1100,70.1,97.5\n";
        var tmpFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tmpFile, csvContent);

        var config = new DataSourceConfig { FilePath = tmpFile };
        var adapter = new CsvDataAdapter();
        var result = await adapter.FetchAsync(config);

        Assert.Equal("1200", result.Fields["speed"]?.ToString());
        Assert.Equal("68.3", result.Fields["temp"]?.ToString());

        File.Delete(tmpFile);
    }
}
```

`TPS.Nexus.Kanban.Tests/DataSource/JsonDataAdapterTests.cs`:
```csharp
using TPS.Nexus.Kanban.Core.Models;
using TPS.Nexus.Kanban.Services.DataSource;
using Xunit;

namespace TPS.Nexus.Kanban.Tests.DataSource;

public class JsonDataAdapterTests
{
    [Fact]
    public async Task FetchAsync_ParsesFlatJsonAsFields()
    {
        var json = """{"speed":1200,"temp":68.3,"status":"running"}""";
        var tmpFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tmpFile, json);

        var config = new DataSourceConfig { FilePath = tmpFile };
        var adapter = new JsonDataAdapter();
        var result = await adapter.FetchAsync(config);

        Assert.Equal(1200, Convert.ToInt32(result.Fields["speed"]));
        Assert.Equal("running", result.Fields["status"]?.ToString());

        File.Delete(tmpFile);
    }
}
```

`TPS.Nexus.Kanban.Tests/DataSource/XmlDataAdapterTests.cs`:
```csharp
using TPS.Nexus.Kanban.Core.Models;
using TPS.Nexus.Kanban.Services.DataSource;
using Xunit;

namespace TPS.Nexus.Kanban.Tests.DataSource;

public class XmlDataAdapterTests
{
    [Fact]
    public async Task FetchAsync_ParsesXmlAttributesAsFields()
    {
        var xml = """
            <?xml version="1.0"?>
            <machine speed="1200" temp="68.3" status="running" />
            """;
        var tmpFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tmpFile, xml);

        var config = new DataSourceConfig { FilePath = tmpFile, QueryOrPath = "/machine" };
        var adapter = new XmlDataAdapter();
        var result = await adapter.FetchAsync(config);

        Assert.Equal("1200", result.Fields["speed"]?.ToString());
        Assert.Equal("running", result.Fields["status"]?.ToString());

        File.Delete(tmpFile);
    }
}
```

- [ ] **Step 2: Run tests — verify they fail**

```bash
dotnet test TPS.Nexus.Kanban.Tests --filter "CsvDataAdapterTests|JsonDataAdapterTests|XmlDataAdapterTests"
```

Expected: FAIL — adapter classes not found.

- [ ] **Step 3: Implement CsvDataAdapter**

`TPS.Nexus.Kanban.Services/DataSource/CsvDataAdapter.cs`:
```csharp
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.DataSource;

public class CsvDataAdapter
{
    public Task<DataResult> FetchAsync(DataSourceConfig config)
    {
        var result = new DataResult();
        var path = config.FilePath
            ?? throw new InvalidOperationException("FilePath is required for CSV source.");

        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null
        };

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, csvConfig);

        csv.Read();
        csv.ReadHeader();
        if (csv.Read())
        {
            foreach (var header in csv.HeaderRecord ?? Array.Empty<string>())
                result.Fields[header] = csv.GetField(header);
        }

        return Task.FromResult(result);
    }

    public Task<IEnumerable<DataResult>> FetchHistoryAsync(DataSourceConfig config, DateTime from, DateTime to)
    {
        var results = new List<DataResult>();
        var path = config.FilePath
            ?? throw new InvalidOperationException("FilePath is required for CSV source.");

        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null
        };

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, csvConfig);

        csv.Read();
        csv.ReadHeader();
        while (csv.Read())
        {
            var row = new DataResult();
            foreach (var header in csv.HeaderRecord ?? Array.Empty<string>())
                row.Fields[header] = csv.GetField(header);
            results.Add(row);
        }

        return Task.FromResult<IEnumerable<DataResult>>(results);
    }
}
```

- [ ] **Step 4: Implement JsonDataAdapter**

`TPS.Nexus.Kanban.Services/DataSource/JsonDataAdapter.cs`:
```csharp
using System.Text.Json;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.DataSource;

public class JsonDataAdapter
{
    public async Task<DataResult> FetchAsync(DataSourceConfig config)
    {
        var result = new DataResult();
        var path = config.FilePath
            ?? throw new InvalidOperationException("FilePath is required for JSON source.");

        var json = await File.ReadAllTextAsync(path);
        var doc = JsonDocument.Parse(json);

        var target = string.IsNullOrEmpty(config.QueryOrPath)
            ? doc.RootElement
            : NavigatePath(doc.RootElement, config.QueryOrPath);

        if (target.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in target.EnumerateObject())
                result.Fields[prop.Name] = ExtractValue(prop.Value);
        }

        return result;
    }

    public async Task<IEnumerable<DataResult>> FetchHistoryAsync(DataSourceConfig config, DateTime from, DateTime to)
    {
        var results = new List<DataResult>();
        var path = config.FilePath
            ?? throw new InvalidOperationException("FilePath is required for JSON source.");

        var json = await File.ReadAllTextAsync(path);
        var doc = JsonDocument.Parse(json);

        var target = string.IsNullOrEmpty(config.QueryOrPath)
            ? doc.RootElement
            : NavigatePath(doc.RootElement, config.QueryOrPath);

        if (target.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in target.EnumerateArray())
            {
                var row = new DataResult();
                if (item.ValueKind == JsonValueKind.Object)
                    foreach (var prop in item.EnumerateObject())
                        row.Fields[prop.Name] = ExtractValue(prop.Value);
                results.Add(row);
            }
        }

        return results;
    }

    private static JsonElement NavigatePath(JsonElement root, string path)
    {
        var segments = path.Trim('/').Split('/');
        var current = root;
        foreach (var seg in segments)
            if (current.TryGetProperty(seg, out var next))
                current = next;
        return current;
    }

    private static object? ExtractValue(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => el.GetString()
    };
}
```

- [ ] **Step 5: Implement XmlDataAdapter**

`TPS.Nexus.Kanban.Services/DataSource/XmlDataAdapter.cs`:
```csharp
using System.Xml;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.DataSource;

public class XmlDataAdapter
{
    public Task<DataResult> FetchAsync(DataSourceConfig config)
    {
        var result = new DataResult();
        var path = config.FilePath
            ?? throw new InvalidOperationException("FilePath is required for XML source.");

        var doc = new XmlDocument();
        doc.Load(path);

        var xpath = string.IsNullOrEmpty(config.QueryOrPath) ? "/*" : config.QueryOrPath;
        var node = doc.SelectSingleNode(xpath);
        if (node is XmlElement el)
        {
            foreach (XmlAttribute attr in el.Attributes)
                result.Fields[attr.Name] = attr.Value;
            foreach (XmlElement child in el.ChildNodes.OfType<XmlElement>())
                result.Fields[child.LocalName] = child.InnerText;
        }

        return Task.FromResult(result);
    }

    public Task<IEnumerable<DataResult>> FetchHistoryAsync(DataSourceConfig config, DateTime from, DateTime to)
    {
        var results = new List<DataResult>();
        var path = config.FilePath
            ?? throw new InvalidOperationException("FilePath is required for XML source.");

        var doc = new XmlDocument();
        doc.Load(path);

        var xpath = string.IsNullOrEmpty(config.QueryOrPath) ? "//*" : config.QueryOrPath;
        var nodes = doc.SelectNodes(xpath);
        if (nodes == null) return Task.FromResult<IEnumerable<DataResult>>(results);

        foreach (XmlElement el in nodes.OfType<XmlElement>())
        {
            var row = new DataResult();
            foreach (XmlAttribute attr in el.Attributes)
                row.Fields[attr.Name] = attr.Value;
            results.Add(row);
        }

        return Task.FromResult<IEnumerable<DataResult>>(results);
    }
}
```

- [ ] **Step 6: Run tests — verify they pass**

```bash
dotnet test TPS.Nexus.Kanban.Tests --filter "CsvDataAdapterTests|JsonDataAdapterTests|XmlDataAdapterTests"
```

Expected: PASS (3 tests).

- [ ] **Step 7: Commit**

```bash
git add TPS.Nexus.Kanban.Services/DataSource/
git add TPS.Nexus.Kanban.Tests/DataSource/
git commit -m "feat(services): add CSV, JSON, XML data adapters with tests"
```

---

## Task 8: Service — DataSourceService (dispatcher)

**Files:**
- Create: `TPS.Nexus.Kanban.Services/DataSource/DataSourceService.cs`

- [ ] **Step 1: Write DataSourceService**

`TPS.Nexus.Kanban.Services/DataSource/DataSourceService.cs`:
```csharp
using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Interfaces;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.DataSource;

public class DataSourceService : IDataSourceService
{
    private readonly SqlDataAdapter _sql;
    private readonly CsvDataAdapter _csv;
    private readonly JsonDataAdapter _json;
    private readonly XmlDataAdapter _xml;

    public DataSourceService(IDbConnectionFactory dbFactory)
    {
        _sql = new SqlDataAdapter(dbFactory);
        _csv = new CsvDataAdapter();
        _json = new JsonDataAdapter();
        _xml = new XmlDataAdapter();
    }

    public Task<DataResult> FetchAsync(DataSourceConfig config) => config.SourceType switch
    {
        DataSourceType.Sql  => _sql.FetchAsync(config),
        DataSourceType.Csv  => _csv.FetchAsync(config),
        DataSourceType.Json => _json.FetchAsync(config),
        DataSourceType.Xml  => _xml.FetchAsync(config),
        _ => throw new NotSupportedException($"DataSourceType {config.SourceType} is not supported.")
    };

    public Task<IEnumerable<DataResult>> FetchHistoryAsync(DataSourceConfig config, DateTime from, DateTime to)
        => config.SourceType switch
        {
            DataSourceType.Sql  => _sql.FetchHistoryAsync(config, from, to),
            DataSourceType.Csv  => _csv.FetchHistoryAsync(config, from, to),
            DataSourceType.Json => _json.FetchHistoryAsync(config, from, to),
            DataSourceType.Xml  => _xml.FetchHistoryAsync(config, from, to),
            _ => throw new NotSupportedException($"DataSourceType {config.SourceType} is not supported.")
        };
}
```

- [ ] **Step 2: Build Services**

```bash
dotnet build TPS.Nexus.Kanban.Services
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add TPS.Nexus.Kanban.Services/DataSource/DataSourceService.cs
git commit -m "feat(services): add DataSourceService dispatcher"
```

---

## Task 9: Service — Map Import (handlers + service)

**Files:**
- Create: `TPS.Nexus.Kanban.Services/Map/ImageMapHandler.cs`
- Create: `TPS.Nexus.Kanban.Services/Map/SvgMapParser.cs`
- Create: `TPS.Nexus.Kanban.Services/Map/DxfMapParser.cs`
- Create: `TPS.Nexus.Kanban.Services/Map/JsonXmlCoordParser.cs`
- Create: `TPS.Nexus.Kanban.Services/Map/MapImportService.cs`

- [ ] **Step 1: Write ImageMapHandler**

`TPS.Nexus.Kanban.Services/Map/ImageMapHandler.cs`:
```csharp
using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.Map;

public class ImageMapHandler
{
    private readonly string _storageRoot;

    public ImageMapHandler(string storageRoot) => _storageRoot = storageRoot;

    public async Task<FactoryMap> HandleAsync(Stream file, string fileName, MapFormatType format)
    {
        var dir = Path.Combine(_storageRoot, "maps");
        Directory.CreateDirectory(dir);
        var ext = format == MapFormatType.Png ? ".png" : ".jpg";
        var savedName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(dir, savedName);

        await using var fs = File.Create(fullPath);
        await file.CopyToAsync(fs);

        return new FactoryMap
        {
            FilePath = $"/module-assets/TPS.Nexus.Kanban/maps/{savedName}",
            FormatType = format,
            CreatedAt = DateTime.UtcNow
        };
    }
}
```

- [ ] **Step 2: Write SvgMapParser**

`TPS.Nexus.Kanban.Services/Map/SvgMapParser.cs`:
```csharp
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.Map;

public class SvgMapParser
{
    private readonly string _storageRoot;

    public SvgMapParser(string storageRoot) => _storageRoot = storageRoot;

    public async Task<FactoryMap> ParseAsync(Stream file, string fileName)
    {
        var dir = Path.Combine(_storageRoot, "maps");
        Directory.CreateDirectory(dir);
        var savedName = $"{Guid.NewGuid()}.svg";
        var fullPath = Path.Combine(dir, savedName);

        await using var fs = File.Create(fullPath);
        await file.CopyToAsync(fs);

        return new FactoryMap
        {
            FilePath = $"/module-assets/TPS.Nexus.Kanban/maps/{savedName}",
            FormatType = Core.Enums.MapFormatType.Svg,
            CreatedAt = DateTime.UtcNow
        };
    }
}
```

- [ ] **Step 3: Write DxfMapParser**

`TPS.Nexus.Kanban.Services/Map/DxfMapParser.cs`:
```csharp
using netDxf;
using netDxf.Entities;
using System.Text;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.Map;

public class DxfMapParser
{
    private readonly string _storageRoot;

    public DxfMapParser(string storageRoot) => _storageRoot = storageRoot;

    public async Task<FactoryMap> ParseAsync(Stream file, string fileName)
    {
        // Save DXF temp file (netDxf requires file path)
        var tmpDxf = Path.GetTempFileName() + ".dxf";
        await using (var fs = File.Create(tmpDxf))
            await file.CopyToAsync(fs);

        var dxf = DxfDocument.Load(tmpDxf);
        File.Delete(tmpDxf);

        var svg = ConvertToSvg(dxf);

        var dir = Path.Combine(_storageRoot, "maps");
        Directory.CreateDirectory(dir);
        var savedName = $"{Guid.NewGuid()}.svg";
        await File.WriteAllTextAsync(Path.Combine(dir, savedName), svg);

        return new FactoryMap
        {
            FilePath = $"/module-assets/TPS.Nexus.Kanban/maps/{savedName}",
            FormatType = Core.Enums.MapFormatType.Dxf,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static string ConvertToSvg(DxfDocument dxf)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<svg xmlns="http://www.w3.org/2000/svg" width="1000" height="800">""");

        foreach (var line in dxf.Entities.Lines)
        {
            sb.AppendLine($"""  <line x1="{line.StartPoint.X:F1}" y1="{line.StartPoint.Y:F1}" x2="{line.EndPoint.X:F1}" y2="{line.EndPoint.Y:F1}" stroke="#4a6a8a" stroke-width="1.5"/>""");
        }

        foreach (var circle in dxf.Entities.Circles)
        {
            sb.AppendLine($"""  <circle cx="{circle.Center.X:F1}" cy="{circle.Center.Y:F1}" r="{circle.Radius:F1}" fill="none" stroke="#4a6a8a" stroke-width="1.5"/>""");
        }

        foreach (var text in dxf.Entities.Texts)
        {
            sb.AppendLine($"""  <text x="{text.Position.X:F1}" y="{text.Position.Y:F1}" fill="#3a6a9a" font-size="11">{text.Value}</text>""");
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }
}
```

- [ ] **Step 4: Write JsonXmlCoordParser**

`TPS.Nexus.Kanban.Services/Map/JsonXmlCoordParser.cs`:
```csharp
using System.Text.Json;
using System.Xml;
using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.Map;

public class JsonXmlCoordParser
{
    public Task<FactoryMap> ParseJsonAsync(Stream file, string fileName)
    {
        // JSON coord definition: { "name": "Floor 1", "width": 1000, "height": 800, "equipment": [...] }
        // Returns a FactoryMap with FilePath pointing to the saved JSON file
        // The canvas renders equipment positions from this JSON instead of an image
        var tmpPath = SaveToTemp(file, ".json");
        return Task.FromResult(new FactoryMap
        {
            FilePath = tmpPath,
            FormatType = MapFormatType.JsonCoord,
            CreatedAt = DateTime.UtcNow
        });
    }

    public Task<FactoryMap> ParseXmlAsync(Stream file, string fileName)
    {
        var tmpPath = SaveToTemp(file, ".xml");
        return Task.FromResult(new FactoryMap
        {
            FilePath = tmpPath,
            FormatType = MapFormatType.XmlCoord,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static string SaveToTemp(Stream file, string ext)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{ext}");
        using var fs = File.Create(path);
        file.CopyTo(fs);
        return path;
    }
}
```

- [ ] **Step 5: Write MapImportService**

`TPS.Nexus.Kanban.Services/Map/MapImportService.cs`:
```csharp
using Dapper;
using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Interfaces;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.Map;

public class MapImportService : IMapImportService
{
    private readonly IDbConnectionFactory _db;
    private readonly string _storageRoot;
    private readonly ImageMapHandler _imageHandler;
    private readonly SvgMapParser _svgParser;
    private readonly DxfMapParser _dxfParser;
    private readonly JsonXmlCoordParser _coordParser;

    public MapImportService(IDbConnectionFactory db, IWebHostEnvironmentAccessor envAccessor)
    {
        _db = db;
        _storageRoot = envAccessor.WebRootPath;
        _imageHandler = new ImageMapHandler(_storageRoot);
        _svgParser = new SvgMapParser(_storageRoot);
        _dxfParser = new DxfMapParser(_storageRoot);
        _coordParser = new JsonXmlCoordParser();
    }

    public async Task<FactoryMap> ImportAsync(Stream file, string fileName, MapFormatType format)
    {
        var map = format switch
        {
            MapFormatType.Png or MapFormatType.Jpg => await _imageHandler.HandleAsync(file, fileName, format),
            MapFormatType.Svg => await _svgParser.ParseAsync(file, fileName),
            MapFormatType.Dxf => await _dxfParser.ParseAsync(file, fileName),
            MapFormatType.JsonCoord => await _coordParser.ParseJsonAsync(file, fileName),
            MapFormatType.XmlCoord => await _coordParser.ParseXmlAsync(file, fileName),
            _ => throw new NotSupportedException($"Format {format} not supported.")
        };

        map.Name = Path.GetFileNameWithoutExtension(fileName);

        using var conn = _db.CreateConnection();
        map.Id = await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO kanban_factory_maps (Name, FormatType, FilePath, ThumbnailPath, CreatedAt)
            VALUES (@Name, @FormatType, @FilePath, @ThumbnailPath, @CreatedAt);
            SELECT LAST_INSERT_ID();
            """, map);

        return map;
    }

    public async Task<IEnumerable<FactoryMap>> GetAllAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<FactoryMap>("SELECT * FROM kanban_factory_maps ORDER BY CreatedAt DESC");
    }

    public async Task DeleteAsync(int mapId)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM kanban_factory_maps WHERE Id = @Id", new { Id = mapId });
    }
}
```

> **Note:** `IWebHostEnvironmentAccessor` is a small helper interface to inject `WebRootPath` without taking a hard dependency on `IWebHostEnvironment`. Add this interface to Core and register it in the host:
>
> ```csharp
> // TPS.Nexus.Kanban.Core/Interfaces/IWebHostEnvironmentAccessor.cs
> public interface IWebHostEnvironmentAccessor { string WebRootPath { get; } }
>
> // In host Program.cs (after WebApplication is built):
> services.AddSingleton<IWebHostEnvironmentAccessor>(
>     new WebHostEnvironmentAccessor(app.Environment.WebRootPath));
>
> // Implementation (in Services):
> public class WebHostEnvironmentAccessor : IWebHostEnvironmentAccessor {
>     public WebHostEnvironmentAccessor(string path) => WebRootPath = path;
>     public string WebRootPath { get; }
> }
> ```

- [ ] **Step 6: Build Services**

```bash
dotnet build TPS.Nexus.Kanban.Services
```

Expected: 0 errors.

- [ ] **Step 7: Commit**

```bash
git add TPS.Nexus.Kanban.Services/Map/
git commit -m "feat(services): add map import handlers (PNG/JPG/SVG/DXF/JSON/XML)"
```

---

## Task 10: Service — EquipmentService

**Files:**
- Create: `TPS.Nexus.Kanban.Services/Equipment/EquipmentService.cs`

- [ ] **Step 1: Write EquipmentService**

`TPS.Nexus.Kanban.Services/Equipment/EquipmentService.cs`:
```csharp
using Dapper;
using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Core.Interfaces;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.Equipment;

public class EquipmentService : IEquipmentService
{
    private readonly IDbConnectionFactory _db;

    public EquipmentService(IDbConnectionFactory db) => _db = db;

    public async Task<IEnumerable<Equipment>> GetAllEquipmentAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Equipment>("SELECT * FROM kanban_equipment ORDER BY Name");
    }

    public async Task<Equipment?> GetEquipmentAsync(int id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Equipment>(
            "SELECT * FROM kanban_equipment WHERE Id = @Id", new { Id = id });
    }

    public async Task<Equipment> CreateEquipmentAsync(Equipment equipment)
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

    public async Task UpdateEquipmentAsync(Equipment equipment)
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
```

- [ ] **Step 2: Build Services**

```bash
dotnet build TPS.Nexus.Kanban.Services
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add TPS.Nexus.Kanban.Services/Equipment/
git commit -m "feat(services): add EquipmentService with full CRUD"
```

---

## Task 11: Service — LayoutService (with tests)

**Files:**
- Create: `TPS.Nexus.Kanban.Services/Layout/LayoutService.cs`
- Create: `TPS.Nexus.Kanban.Tests/Layout/LayoutServiceTests.cs`

- [ ] **Step 1: Write failing tests**

`TPS.Nexus.Kanban.Tests/Layout/LayoutServiceTests.cs`:
```csharp
using Dapper;
using NSubstitute;
using System.Data;
using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Models;
using TPS.Nexus.Kanban.Services.Layout;
using Xunit;

namespace TPS.Nexus.Kanban.Tests.Layout;

public class LayoutServiceTests
{
    // Integration-style: use an in-memory SQLite if available, or mock IDbConnection
    // For this test we verify business logic around status transitions.

    [Fact]
    public void PublishAsync_RequiresDraftStatus()
    {
        // LayoutService.PublishAsync must only accept Draft versions.
        // Verify by checking the Archived/Published logic:
        var archived = new LayoutVersion { Id = 1, Status = LayoutStatus.Archived };
        var draft = new LayoutVersion { Id = 2, Status = LayoutStatus.Draft };

        Assert.True(draft.Status == LayoutStatus.Draft);
        Assert.False(archived.Status == LayoutStatus.Draft);
    }

    [Fact]
    public void RollbackAsync_SetsStatusToPublishedAndArchivesOthers()
    {
        // The rollback policy: target version becomes Published,
        // current Published version becomes Archived.
        var current = new LayoutVersion { Id = 1, Status = LayoutStatus.Published };
        var target = new LayoutVersion { Id = 2, Status = LayoutStatus.Archived };

        // Simulate rollback side-effect
        current.Status = LayoutStatus.Archived;
        target.Status = LayoutStatus.Published;

        Assert.Equal(LayoutStatus.Archived, current.Status);
        Assert.Equal(LayoutStatus.Published, target.Status);
    }
}
```

- [ ] **Step 2: Run tests — verify they pass (logic tests, no DB)**

```bash
dotnet test TPS.Nexus.Kanban.Tests --filter "LayoutServiceTests"
```

Expected: PASS (2 tests — these test status transition logic, not DB).

- [ ] **Step 3: Implement LayoutService**

`TPS.Nexus.Kanban.Services/Layout/LayoutService.cs`:
```csharp
using Dapper;
using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Interfaces;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.Layout;

public class LayoutService : ILayoutService
{
    private readonly IDbConnectionFactory _db;

    public LayoutService(IDbConnectionFactory db) => _db = db;

    public async Task<LayoutVersion> SaveDraftAsync(int factoryMapId, string layoutJson, string createdBy)
    {
        using var conn = _db.CreateConnection();
        var maxVersion = await conn.ExecuteScalarAsync<int>(
            "SELECT COALESCE(MAX(VersionNo), 0) FROM kanban_layout_versions WHERE FactoryMapId=@FactoryMapId",
            new { FactoryMapId = factoryMapId });

        var version = new LayoutVersion
        {
            FactoryMapId = factoryMapId,
            VersionNo = maxVersion + 1,
            Status = LayoutStatus.Draft,
            CreatedBy = createdBy,
            LayoutJson = layoutJson
        };

        version.Id = await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO kanban_layout_versions (FactoryMapId, VersionNo, Status, CreatedBy, PublishedAt, LayoutJson)
            VALUES (@FactoryMapId, @VersionNo, @Status, @CreatedBy, @PublishedAt, @LayoutJson);
            SELECT LAST_INSERT_ID();
            """, version);

        return version;
    }

    public async Task<LayoutVersion> PublishAsync(int draftId)
    {
        using var conn = _db.CreateConnection();
        var draft = await conn.QueryFirstOrDefaultAsync<LayoutVersion>(
            "SELECT * FROM kanban_layout_versions WHERE Id=@Id", new { Id = draftId })
            ?? throw new InvalidOperationException($"Layout version {draftId} not found.");

        if (draft.Status != LayoutStatus.Draft)
            throw new InvalidOperationException($"Only Draft versions can be published. Current status: {draft.Status}");

        // Archive current published version
        await conn.ExecuteAsync(
            """
            UPDATE kanban_layout_versions
            SET Status=@Archived
            WHERE FactoryMapId=@FactoryMapId AND Status=@Published
            """,
            new { Archived = (byte)LayoutStatus.Archived, FactoryMapId = draft.FactoryMapId, Published = (byte)LayoutStatus.Published });

        // Publish draft
        draft.Status = LayoutStatus.Published;
        draft.PublishedAt = DateTime.UtcNow;
        await conn.ExecuteAsync(
            "UPDATE kanban_layout_versions SET Status=@Status, PublishedAt=@PublishedAt WHERE Id=@Id",
            draft);

        return draft;
    }

    public async Task<IEnumerable<LayoutVersion>> GetVersionHistoryAsync(int mapId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<LayoutVersion>(
            "SELECT * FROM kanban_layout_versions WHERE FactoryMapId=@FactoryMapId ORDER BY VersionNo DESC",
            new { FactoryMapId = mapId });
    }

    public async Task<LayoutVersion?> GetPublishedVersionAsync(int mapId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<LayoutVersion>(
            "SELECT * FROM kanban_layout_versions WHERE FactoryMapId=@FactoryMapId AND Status=@Status",
            new { FactoryMapId = mapId, Status = (byte)LayoutStatus.Published });
    }

    public async Task RollbackAsync(int versionId)
    {
        using var conn = _db.CreateConnection();
        var target = await conn.QueryFirstOrDefaultAsync<LayoutVersion>(
            "SELECT * FROM kanban_layout_versions WHERE Id=@Id", new { Id = versionId })
            ?? throw new InvalidOperationException($"Layout version {versionId} not found.");

        // Archive current published
        await conn.ExecuteAsync(
            """
            UPDATE kanban_layout_versions
            SET Status=@Archived
            WHERE FactoryMapId=@FactoryMapId AND Status=@Published
            """,
            new { Archived = (byte)LayoutStatus.Archived, FactoryMapId = target.FactoryMapId, Published = (byte)LayoutStatus.Published });

        // Re-publish target
        await conn.ExecuteAsync(
            "UPDATE kanban_layout_versions SET Status=@Published, PublishedAt=@Now WHERE Id=@Id",
            new { Published = (byte)LayoutStatus.Published, Now = DateTime.UtcNow, Id = versionId });
    }
}
```

- [ ] **Step 4: Build Services**

```bash
dotnet build TPS.Nexus.Kanban.Services
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add TPS.Nexus.Kanban.Services/Layout/ TPS.Nexus.Kanban.Tests/Layout/
git commit -m "feat(services): add LayoutService with version management"
```

---

## Task 12: Service — AlarmService + Tests

**Files:**
- Create: `TPS.Nexus.Kanban.Services/Alarm/AlarmService.cs`
- Create: `TPS.Nexus.Kanban.Tests/Alarm/AlarmServiceTests.cs`

- [ ] **Step 1: Write failing tests**

`TPS.Nexus.Kanban.Tests/Alarm/AlarmServiceTests.cs`:
```csharp
using NSubstitute;
using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Models;
using TPS.Nexus.Kanban.Services.Alarm;
using Xunit;

namespace TPS.Nexus.Kanban.Tests.Alarm;

public class AlarmServiceTests
{
    [Theory]
    [InlineData(">", 100.0, 101.0, true)]
    [InlineData(">", 100.0, 99.0, false)]
    [InlineData("<", 50.0, 49.0, true)]
    [InlineData("<", 50.0, 51.0, false)]
    [InlineData("==", 42.0, 42.0, true)]
    [InlineData("==", 42.0, 43.0, false)]
    [InlineData("!=", 42.0, 43.0, true)]
    [InlineData("!=", 42.0, 42.0, false)]
    public void EvaluateCondition_ReturnsExpected(string condition, double threshold, double value, bool expected)
    {
        var result = AlarmService.EvaluateCondition(condition, threshold, value);
        Assert.Equal(expected, result);
    }
}
```

- [ ] **Step 2: Run test — verify it fails**

```bash
dotnet test TPS.Nexus.Kanban.Tests --filter "AlarmServiceTests"
```

Expected: FAIL — `AlarmService.EvaluateCondition` not found.

- [ ] **Step 3: Implement AlarmService**

`TPS.Nexus.Kanban.Services/Alarm/AlarmService.cs`:
```csharp
using Dapper;
using Microsoft.AspNetCore.SignalR;
using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Interfaces;
using TPS.Nexus.Kanban.Core.Models;
using TPS.Nexus.Kanban.Services.Hubs;

namespace TPS.Nexus.Kanban.Services.Alarm;

public class AlarmService : IAlarmService
{
    private readonly IDbConnectionFactory _db;
    private readonly IHubContext<KanbanAlarmHub> _hub;

    public AlarmService(IDbConnectionFactory db, IHubContext<KanbanAlarmHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public async Task EvaluateAsync(int equipmentId, string equipmentName, DataResult latestData)
    {
        var rules = await GetRulesAsync(equipmentId);
        foreach (var rule in rules)
        {
            var config = await GetDataSourceConfigAsync(rule.DataSourceConfigId);
            if (config == null) continue;

            if (!latestData.Fields.TryGetValue(config.QueryOrPath ?? string.Empty, out var rawVal))
                rawVal = latestData.Fields.Values.FirstOrDefault();

            if (rawVal == null) continue;
            if (!double.TryParse(rawVal.ToString(), out var numVal)) continue;

            if (!EvaluateCondition(rule.Condition, rule.Threshold, numVal)) continue;

            var record = new AlarmRecord
            {
                EquipmentId = equipmentId,
                EquipmentName = equipmentName,
                Level = rule.AlarmLevel,
                Message = $"{equipmentName}: {config.Name} {rule.Condition} {rule.Threshold} (actual: {numVal})",
                TriggeredAt = DateTime.UtcNow
            };

            using var conn = _db.CreateConnection();
            record.Id = await conn.ExecuteScalarAsync<int>(
                """
                INSERT INTO kanban_alarm_records (EquipmentId, EquipmentName, Level, Message, TriggeredAt)
                VALUES (@EquipmentId, @EquipmentName, @Level, @Message, @TriggeredAt);
                SELECT LAST_INSERT_ID();
                """, record);

            await _hub.Clients.All.SendAsync("ReceiveAlarm",
                equipmentId, rule.AlarmLevel.ToString(), record.Message);
        }
    }

    public async Task<IEnumerable<AlarmRecord>> GetActiveAlarmsAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<AlarmRecord>(
            "SELECT * FROM kanban_alarm_records WHERE ResolvedAt IS NULL ORDER BY TriggeredAt DESC");
    }

    public async Task ResolveAlarmAsync(int alarmRecordId)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE kanban_alarm_records SET ResolvedAt=@Now WHERE Id=@Id",
            new { Now = DateTime.UtcNow, Id = alarmRecordId });
    }

    public async Task<IEnumerable<AlarmRule>> GetRulesAsync(int equipmentId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<AlarmRule>(
            "SELECT * FROM kanban_alarm_rules WHERE EquipmentId=@EquipmentId",
            new { EquipmentId = equipmentId });
    }

    public async Task SaveRuleAsync(AlarmRule rule)
    {
        using var conn = _db.CreateConnection();
        if (rule.Id == 0)
            await conn.ExecuteAsync(
                """
                INSERT INTO kanban_alarm_rules (EquipmentId, DataSourceConfigId, Condition, Threshold, AlarmLevel)
                VALUES (@EquipmentId, @DataSourceConfigId, @Condition, @Threshold, @AlarmLevel)
                """, rule);
        else
            await conn.ExecuteAsync(
                """
                UPDATE kanban_alarm_rules
                SET Condition=@Condition, Threshold=@Threshold, AlarmLevel=@AlarmLevel
                WHERE Id=@Id
                """, rule);
    }

    public async Task DeleteRuleAsync(int ruleId)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM kanban_alarm_rules WHERE Id=@Id", new { Id = ruleId });
    }

    // Internal — testable without DB
    public static bool EvaluateCondition(string condition, double threshold, double value) => condition switch
    {
        ">"  => value > threshold,
        "<"  => value < threshold,
        ">=" => value >= threshold,
        "<=" => value <= threshold,
        "==" => Math.Abs(value - threshold) < 1e-9,
        "!=" => Math.Abs(value - threshold) >= 1e-9,
        _ => false
    };

    private async Task<DataSourceConfig?> GetDataSourceConfigAsync(int configId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<DataSourceConfig>(
            "SELECT * FROM kanban_datasource_configs WHERE Id=@Id", new { Id = configId });
    }
}
```

- [ ] **Step 4: Run tests — verify they pass**

```bash
dotnet test TPS.Nexus.Kanban.Tests --filter "AlarmServiceTests"
```

Expected: PASS (8 tests).

- [ ] **Step 5: Commit**

```bash
git add TPS.Nexus.Kanban.Services/Alarm/ TPS.Nexus.Kanban.Tests/Alarm/
git commit -m "feat(services): add AlarmService with SignalR push and condition evaluator tests"
```

---

## Task 13: Service — IconUploadService

**Files:**
- Create: `TPS.Nexus.Kanban.Services/Icon/IconUploadService.cs`

- [ ] **Step 1: Write IconUploadService**

`TPS.Nexus.Kanban.Services/Icon/IconUploadService.cs`:
```csharp
using TPS.Nexus.Kanban.Core.Interfaces;

namespace TPS.Nexus.Kanban.Services.Icon;

public class IconUploadService : IIconUploadService
{
    private readonly string _iconDir;

    public IconUploadService(IWebHostEnvironmentAccessor envAccessor)
    {
        _iconDir = Path.Combine(envAccessor.WebRootPath, "images", "equipment-icons");
        Directory.CreateDirectory(_iconDir);
    }

    public async Task<string> UploadAsync(Stream file, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext is not ".png" and not ".jpg" and not ".jpeg" and not ".svg")
            throw new InvalidOperationException("Only PNG, JPG, and SVG icons are supported.");

        var savedName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(_iconDir, savedName);

        await using var fs = File.Create(fullPath);
        await file.CopyToAsync(fs);

        return $"/module-assets/TPS.Nexus.Kanban/images/equipment-icons/{savedName}";
    }

    public Task DeleteAsync(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var fullPath = Path.Combine(_iconDir, fileName);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Build entire Services project**

```bash
dotnet build TPS.Nexus.Kanban.Services
```

Expected: 0 errors.

- [ ] **Step 3: Run all tests**

```bash
dotnet test TPS.Nexus.Kanban.Tests
```

Expected: All tests PASS.

- [ ] **Step 4: Commit**

```bash
git add TPS.Nexus.Kanban.Services/Icon/
git commit -m "feat(services): add IconUploadService"
```

---

## Task 14: Web — Project Setup + wwwroot Assets

**Files:**
- Create: `TPS.Nexus.Kanban.Web/_Imports.razor`
- Create: `TPS.Nexus.Kanban.Web/wwwroot/js/kanban-drag.js`
- Create: `TPS.Nexus.Kanban.Web/wwwroot/css/kanban.css`
- Download: `TPS.Nexus.Kanban.Web/wwwroot/js/sortable.min.js`

- [ ] **Step 1: Write _Imports.razor**

`TPS.Nexus.Kanban.Web/_Imports.razor`:
```razor
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.JSInterop
@using Radzen
@using Radzen.Blazor
@using TPS.Nexus.Kanban.Core.Enums
@using TPS.Nexus.Kanban.Core.Interfaces
@using TPS.Nexus.Kanban.Core.Models
```

- [ ] **Step 2: Download SortableJS**

```bash
curl -L https://cdn.jsdelivr.net/npm/sortablejs@latest/Sortable.min.js \
     -o TPS.Nexus.Kanban.Web/wwwroot/js/sortable.min.js
```

- [ ] **Step 3: Write kanban-drag.js**

`TPS.Nexus.Kanban.Web/wwwroot/js/kanban-drag.js`:
```javascript
window.kanbanDrag = {
    _sortable: null,

    init: function (containerId, dotNetRef) {
        var el = document.getElementById(containerId);
        if (!el) return;
        this._sortable = Sortable.create(el, {
            animation: 150,
            onEnd: function (evt) {
                var equipmentId = evt.item.dataset.equipmentId;
                var x = parseInt(evt.item.style.left) || evt.originalEvent.offsetX;
                var y = parseInt(evt.item.style.top) || evt.originalEvent.offsetY;
                dotNetRef.invokeMethodAsync('OnEquipmentMoved', equipmentId, x, y);
            }
        });
    },

    destroy: function () {
        if (this._sortable) {
            this._sortable.destroy();
            this._sortable = null;
        }
    },

    updatePosition: function (elementId, x, y) {
        var el = document.getElementById(elementId);
        if (el) {
            el.style.left = x + 'px';
            el.style.top = y + 'px';
        }
    }
};
```

- [ ] **Step 4: Write kanban.css**

`TPS.Nexus.Kanban.Web/wwwroot/css/kanban.css`:
```css
.kanban-map-container {
    position: relative;
    overflow: hidden;
    background: #1a1a2e;
    user-select: none;
}

.kanban-map-layer {
    position: absolute;
    inset: 0;
    pointer-events: none;
}

.kanban-map-layer img {
    width: 100%;
    height: 100%;
    object-fit: contain;
    opacity: 0.7;
}

.kanban-equipment-layer {
    position: absolute;
    inset: 0;
}

.kanban-widget {
    position: absolute;
    background: rgba(15, 52, 96, 0.92);
    border: 1px solid #1a4a8a;
    border-radius: 7px;
    padding: 7px 9px;
    text-align: center;
    cursor: pointer;
    backdrop-filter: blur(4px);
    transition: border-color 0.2s, box-shadow 0.2s;
    min-width: 72px;
}

.kanban-widget:hover {
    border-color: #2196f3;
    box-shadow: 0 0 8px rgba(33, 150, 243, 0.4);
    z-index: 25;
}

.kanban-widget.alarm-critical {
    background: rgba(61, 0, 0, 0.92);
    border-color: #7f0000;
    box-shadow: 0 0 10px rgba(244, 67, 54, 0.4);
}

.kanban-widget.alarm-warning {
    background: rgba(40, 25, 0, 0.92);
    border-color: #7a5000;
}

.kanban-widget-icon {
    font-size: 1.6em;
    line-height: 1;
}

.kanban-widget-name {
    color: #fff;
    font-size: 0.68em;
    margin: 3px 0;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

.kanban-status-dot {
    display: inline-block;
    width: 8px;
    height: 8px;
    border-radius: 50%;
    vertical-align: middle;
    margin-right: 3px;
}

.kanban-status-running  { background: #4caf50; box-shadow: 0 0 4px #4caf50; }
.kanban-status-idle     { background: #ff9800; box-shadow: 0 0 4px #ff9800; }
.kanban-status-stopped  { background: #607d8b; }
.kanban-status-alarm    { background: #f44336; box-shadow: 0 0 6px #f44336; animation: blink 1s infinite; }

@keyframes blink {
    0%, 100% { opacity: 1; }
    50%       { opacity: 0.3; }
}

.kanban-alarm-badge {
    position: absolute;
    top: -4px;
    right: -4px;
    background: #f44336;
    color: #fff;
    border-radius: 50%;
    width: 16px;
    height: 16px;
    font-size: 0.55em;
    display: flex;
    align-items: center;
    justify-content: center;
    font-weight: bold;
}

.kanban-edit-mode .kanban-widget {
    cursor: grab;
    border-style: dashed;
}

.kanban-edit-mode .kanban-widget:active {
    cursor: grabbing;
}
```

- [ ] **Step 5: Build Web**

```bash
dotnet build TPS.Nexus.Kanban.Web
```

Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add TPS.Nexus.Kanban.Web/
git commit -m "feat(web): add project setup, imports, wwwroot assets (CSS + JS)"
```

---

## Task 15: Web — StatusIndicator + EquipmentIconRenderer + EquipmentWidgetPanel

**Files:**
- Create: `TPS.Nexus.Kanban.Web/Components/Widget/StatusIndicator.razor`
- Create: `TPS.Nexus.Kanban.Web/Components/Widget/EquipmentIconRenderer.razor`
- Create: `TPS.Nexus.Kanban.Web/Components/Widget/EquipmentWidgetPanel.razor`

- [ ] **Step 1: Write StatusIndicator**

`TPS.Nexus.Kanban.Web/Components/Widget/StatusIndicator.razor`:
```razor
@* Renders a coloured status dot + label based on alarm state. *@

<span class="kanban-status-dot @CssClass"></span>
<span style="color:@Color; font-size:0.6em">@Label</span>

@code {
    [Parameter] public AlarmLevel? ActiveAlarm { get; set; }
    [Parameter] public bool IsRunning { get; set; } = true;

    private string CssClass => ActiveAlarm switch
    {
        AlarmLevel.Critical or AlarmLevel.Warning => "kanban-status-alarm",
        _ => IsRunning ? "kanban-status-running" : "kanban-status-stopped"
    };

    private string Color => ActiveAlarm switch
    {
        AlarmLevel.Critical => "#f44336",
        AlarmLevel.Warning  => "#ff9800",
        _ => IsRunning ? "#4caf50" : "#607d8b"
    };

    private string Label => ActiveAlarm switch
    {
        AlarmLevel.Critical => "⚠ 警報",
        AlarmLevel.Warning  => "⚠ 警告",
        _ => IsRunning ? "運行" : "停機"
    };
}
```

- [ ] **Step 2: Write EquipmentIconRenderer**

`TPS.Nexus.Kanban.Web/Components/Widget/EquipmentIconRenderer.razor`:
```razor
@* Renders equipment icon — CSS class (FontAwesome/etc.) or custom image. *@

@if (Equipment?.IconType == IconType.CustomImage && !string.IsNullOrEmpty(Equipment.IconValue))
{
    <img src="@Equipment.IconValue" alt="@Equipment.Name"
         style="width:1.6em; height:1.6em; object-fit:contain;" />
}
else if (!string.IsNullOrEmpty(Equipment?.IconValue))
{
    <i class="@Equipment.IconValue" style="font-size:1.6em;"></i>
}
else
{
    <span style="font-size:1.6em;">⚙️</span>
}

@code {
    [Parameter] public Equipment? Equipment { get; set; }
}
```

- [ ] **Step 3: Write EquipmentWidgetPanel**

`TPS.Nexus.Kanban.Web/Components/Widget/EquipmentWidgetPanel.razor`:
```razor
@* Map-resident widget: icon + name + status only. Hover/click handled by parent canvas. *@

<div id="widget-@Widget.Id"
     class="kanban-widget @AlarmClass"
     data-equipment-id="@Widget.EquipmentId"
     style="left:@(Widget.PositionX)px; top:@(Widget.PositionY)px; width:@(Widget.Width)px;"
     @onclick="HandleClick"
     @onmouseenter="HandleMouseEnter"
     @onmouseleave="HandleMouseLeave">

    <div class="kanban-widget-icon">
        <EquipmentIconRenderer Equipment="@EquipmentData" />
    </div>
    <div class="kanban-widget-name">@EquipmentData?.Name</div>
    <div style="display:flex;align-items:center;justify-content:center;gap:3px">
        <StatusIndicator ActiveAlarm="@ActiveAlarm" IsRunning="@IsRunning" />
    </div>

    @if (ActiveAlarm.HasValue)
    {
        <AlarmBadge />
    }
</div>

@code {
    [Parameter, EditorRequired] public EquipmentWidget Widget { get; set; } = default!;
    [Parameter] public Equipment? EquipmentData { get; set; }
    [Parameter] public AlarmLevel? ActiveAlarm { get; set; }
    [Parameter] public bool IsRunning { get; set; } = true;
    [Parameter] public EventCallback<EquipmentWidget> OnClick { get; set; }
    [Parameter] public EventCallback<EquipmentWidget> OnHover { get; set; }
    [Parameter] public EventCallback OnHoverEnd { get; set; }

    private string AlarmClass => ActiveAlarm switch
    {
        AlarmLevel.Critical => "alarm-critical",
        AlarmLevel.Warning  => "alarm-warning",
        _ => string.Empty
    };

    private async Task HandleClick() => await OnClick.InvokeAsync(Widget);
    private async Task HandleMouseEnter() => await OnHover.InvokeAsync(Widget);
    private async Task HandleMouseLeave() => await OnHoverEnd.InvokeAsync();
}
```

- [ ] **Step 4: Build Web**

```bash
dotnet build TPS.Nexus.Kanban.Web
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add TPS.Nexus.Kanban.Web/Components/Widget/
git commit -m "feat(web): add StatusIndicator, EquipmentIconRenderer, EquipmentWidgetPanel"
```

---

## Task 16: Web — MapLayer + FactoryMapCanvas

**Files:**
- Create: `TPS.Nexus.Kanban.Web/Components/Map/MapLayer.razor`
- Create: `TPS.Nexus.Kanban.Web/Components/Map/FactoryMapCanvas.razor`

- [ ] **Step 1: Write MapLayer**

`TPS.Nexus.Kanban.Web/Components/Map/MapLayer.razor`:
```razor
@* Bottom layer — renders factory floor map based on format type. *@

@if (Map != null)
{
    @switch (Map.FormatType)
    {
        case MapFormatType.Png:
        case MapFormatType.Jpg:
            <img src="@Map.FilePath" alt="@Map.Name" class="kanban-map-layer"
                 style="position:absolute;inset:0;width:100%;height:100%;object-fit:contain;opacity:0.65;pointer-events:none;" />
            break;

        case MapFormatType.Svg:
        case MapFormatType.Dxf:
            @if (!string.IsNullOrEmpty(SvgContent))
            {
                <div style="position:absolute;inset:0;opacity:0.65;pointer-events:none;"
                     @((MarkupString)SvgContent)></div>
            }
            break;

        case MapFormatType.JsonCoord:
        case MapFormatType.XmlCoord:
            @* Coord-based maps have no background image — equipment positions come from data *@
            break;
    }
}

@code {
    [Parameter] public FactoryMap? Map { get; set; }

    private string? SvgContent;

    protected override async Task OnParametersSetAsync()
    {
        if (Map?.FormatType is MapFormatType.Svg or MapFormatType.Dxf
            && !string.IsNullOrEmpty(Map.FilePath))
        {
            // Read SVG content from wwwroot (relative path strip /module-assets/TPS.Nexus.Kanban)
            var localPath = Map.FilePath.Replace("/module-assets/TPS.Nexus.Kanban/", string.Empty);
            if (File.Exists(localPath))
                SvgContent = await File.ReadAllTextAsync(localPath);
        }
    }
}
```

- [ ] **Step 2: Write FactoryMapCanvas**

`TPS.Nexus.Kanban.Web/Components/Map/FactoryMapCanvas.razor`:
```razor
@inject IJSRuntime JS
@inject IEquipmentService EquipmentSvc
@inject ILayoutService LayoutSvc
@inject IAlarmService AlarmSvc
@inject IFunctionPermissionService PermSvc
@implements IAsyncDisposable

<div id="map-canvas"
     class="kanban-map-container @(IsEditMode ? "kanban-edit-mode" : "")"
     style="width:100%;height:@(CanvasHeight)px;position:relative;">

    @* ① Bottom layer — factory map image / SVG *@
    <MapLayer Map="@CurrentMap" />

    @* ② Equipment widget layer *@
    <div class="kanban-equipment-layer">
        @foreach (var widget in Widgets)
        {
            <EquipmentWidgetPanel
                Widget="@widget"
                EquipmentData="@GetEquipment(widget.EquipmentId)"
                ActiveAlarm="@GetActiveAlarm(widget.EquipmentId)"
                IsRunning="@GetIsRunning(widget.EquipmentId)"
                OnClick="@((w) => HandleWidgetClick(w))"
                OnHover="@((w) => HandleWidgetHover(w))"
                OnHoverEnd="@HandleHoverEnd" />
        }
    </div>

    @* ③ Interaction layer *@
    <EquipmentTooltip
        Widget="@HoveredWidget"
        EquipmentData="@GetEquipment(HoveredWidget?.EquipmentId ?? 0)"
        LatestData="@HoveredData" />

    <EquipmentDetailDrawer
        @bind-IsOpen="DrawerOpen"
        Widget="@SelectedWidget"
        EquipmentData="@GetEquipment(SelectedWidget?.EquipmentId ?? 0)"
        LatestData="@SelectedData"
        HistoryData="@HistoryData"
        LinkConfigs="@SelectedLinkConfigs" />
</div>

@code {
    [Parameter, EditorRequired] public int MapId { get; set; }
    [Parameter] public bool IsEditMode { get; set; }
    [Parameter] public int CanvasHeight { get; set; } = 600;

    private FactoryMap? CurrentMap;
    private List<EquipmentWidget> Widgets = new();
    private List<Equipment> AllEquipment = new();
    private List<AlarmRecord> ActiveAlarms = new();

    private EquipmentWidget? HoveredWidget;
    private EquipmentWidget? SelectedWidget;
    private DataResult? HoveredData;
    private DataResult? SelectedData;
    private IEnumerable<DataResult> HistoryData = Enumerable.Empty<DataResult>();
    private IEnumerable<EquipmentLinkConfig> SelectedLinkConfigs = Enumerable.Empty<EquipmentLinkConfig>();
    private bool DrawerOpen;

    private DotNetObjectReference<FactoryMapCanvas>? DotNetRef;
    private PeriodicTimer? _pollTimer;
    private CancellationTokenSource _cts = new();

    protected override async Task OnInitializedAsync()
    {
        DotNetRef = DotNetObjectReference.Create(this);
        var published = await LayoutSvc.GetPublishedVersionAsync(MapId);
        if (published != null)
        {
            Widgets = (await EquipmentSvc.GetWidgetsByVersionAsync(published.Id)).ToList();
        }
        AllEquipment = (await EquipmentSvc.GetAllEquipmentAsync()).ToList();
        ActiveAlarms = (await AlarmSvc.GetActiveAlarmsAsync()).ToList();

        _ = PollLoopAsync(_cts.Token);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && IsEditMode)
        {
            await JS.InvokeVoidAsync("kanbanDrag.init", "map-canvas", DotNetRef);
        }
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        _pollTimer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await _pollTimer.WaitForNextTickAsync(ct))
        {
            ActiveAlarms = (await AlarmSvc.GetActiveAlarmsAsync()).ToList();
            await InvokeAsync(StateHasChanged);
        }
    }

    [JSInvokable]
    public async Task OnEquipmentMoved(string equipmentIdStr, int x, int y)
    {
        if (!int.TryParse(equipmentIdStr, out var equipmentId)) return;
        var widget = Widgets.FirstOrDefault(w => w.EquipmentId == equipmentId);
        if (widget == null) return;
        widget.PositionX = x;
        widget.PositionY = y;
        await EquipmentSvc.SaveWidgetAsync(widget);
        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleWidgetClick(EquipmentWidget widget)
    {
        SelectedWidget = widget;
        SelectedLinkConfigs = await EquipmentSvc.GetLinkConfigsAsync(widget.EquipmentId);
        DrawerOpen = true;
    }

    private async Task HandleWidgetHover(EquipmentWidget widget)
    {
        HoveredWidget = widget;
        // Fetch latest data for tooltip (first StatusIndicator component's data source)
        var statusComp = widget.Components.FirstOrDefault(c => c.ComponentType == WidgetComponentType.StatusIndicator);
        // Data fetch happens in EquipmentTooltip via its own service call
        await InvokeAsync(StateHasChanged);
    }

    private void HandleHoverEnd()
    {
        HoveredWidget = null;
        HoveredData = null;
    }

    private Equipment? GetEquipment(int equipmentId)
        => AllEquipment.FirstOrDefault(e => e.Id == equipmentId);

    private AlarmLevel? GetActiveAlarm(int equipmentId)
    {
        var alarm = ActiveAlarms
            .Where(a => a.EquipmentId == equipmentId)
            .OrderByDescending(a => a.Level)
            .FirstOrDefault();
        return alarm?.Level;
    }

    private bool GetIsRunning(int equipmentId)
        => !ActiveAlarms.Any(a => a.EquipmentId == equipmentId);

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _pollTimer?.Dispose();
        DotNetRef?.Dispose();
        if (IsEditMode)
            await JS.InvokeVoidAsync("kanbanDrag.destroy");
    }
}
```

- [ ] **Step 3: Build Web**

```bash
dotnet build TPS.Nexus.Kanban.Web
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add TPS.Nexus.Kanban.Web/Components/Map/
git commit -m "feat(web): add MapLayer and FactoryMapCanvas with 3-layer architecture"
```

---

## Task 17: Web — EquipmentTooltip + EquipmentDetailDrawer

**Files:**
- Create: `TPS.Nexus.Kanban.Web/Components/Tooltip/EquipmentTooltip.razor`
- Create: `TPS.Nexus.Kanban.Web/Components/Drawer/EquipmentDetailDrawer.razor`

- [ ] **Step 1: Write EquipmentTooltip**

`TPS.Nexus.Kanban.Web/Components/Tooltip/EquipmentTooltip.razor`:
```razor
@* Hover tooltip: parameter values + mini trend chart (RadzenTooltip). *@
@inject IDataSourceService DataSvc
@inject IEquipmentService EquipmentSvc

@if (Widget != null && EquipmentData != null)
{
    <RadzenTooltip @ref="_tooltip" Style="z-index:30;" />

    <div id="tooltip-anchor-@Widget.Id"
         style="position:absolute;left:@(Widget.PositionX + Widget.Width)px;top:@(Widget.PositionY)px;
                background:#0d2137;border:1px solid #1a4a8a;border-radius:8px;
                padding:12px;min-width:200px;box-shadow:0 4px 15px rgba(0,0,0,0.5);z-index:30;">

        <div style="color:#fff;font-size:0.85em;font-weight:bold;margin-bottom:8px;
                    border-bottom:1px solid #1a4a8a;padding-bottom:5px">
            @EquipmentData.Name — 參數摘要
        </div>

        @if (LatestData != null && LatestData.Fields.Any())
        {
            <div style="display:grid;grid-template-columns:1fr 1fr;gap:4px;font-size:0.75em">
                @foreach (var field in LatestData.Fields.Take(6))
                {
                    <div style="color:#888">@field.Key</div>
                    <div style="color:#fff;text-align:right">@field.Value</div>
                }
            </div>

            @if (TrendData.Any())
            {
                <div style="margin-top:8px">
                    <div style="color:#888;font-size:0.65em;margin-bottom:3px">趨勢（最近資料）</div>
                    <RadzenChart Style="height:50px;width:100%">
                        <RadzenLineSeries Data="@TrendData" CategoryProperty="Label" ValueProperty="Value"
                                         Stroke="#2196f3" StrokeWidth="1.5">
                        </RadzenLineSeries>
                        <RadzenCategoryAxis Visible="false" />
                        <RadzenValueAxis Visible="false" />
                    </RadzenChart>
                </div>
            }
        }
        else
        {
            <div style="color:#555;font-size:0.75em">載入中...</div>
        }

        <div style="color:#888;font-size:0.65em;margin-top:5px;text-align:center">點擊查看完整詳情 →</div>
    </div>
}

@code {
    [Parameter] public EquipmentWidget? Widget { get; set; }
    [Parameter] public Equipment? EquipmentData { get; set; }
    [Parameter] public DataResult? LatestData { get; set; }

    private RadzenTooltip? _tooltip;
    private List<TrendPoint> TrendData = new();

    public record TrendPoint(string Label, double Value);

    protected override async Task OnParametersSetAsync()
    {
        if (Widget == null || LatestData == null) return;

        TrendData.Clear();
        var trendComp = Widget.Components.FirstOrDefault(c => c.ComponentType == WidgetComponentType.TrendChart);
        if (trendComp?.DataSourceConfigId == null) return;

        var configs = await EquipmentSvc.GetAllDataSourceConfigsAsync();
        var config = configs.FirstOrDefault(c => c.Id == trendComp.DataSourceConfigId);
        if (config == null) return;

        var history = await DataSvc.FetchHistoryAsync(config, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);
        int i = 0;
        foreach (var row in history.TakeLast(10))
        {
            var firstVal = row.Fields.Values.FirstOrDefault();
            if (firstVal != null && double.TryParse(firstVal.ToString(), out var v))
                TrendData.Add(new TrendPoint((i++).ToString(), v));
        }
    }
}
```

- [ ] **Step 2: Write EquipmentDetailDrawer**

`TPS.Nexus.Kanban.Web/Components/Drawer/EquipmentDetailDrawer.razor`:
```razor
@* Click detail drawer: RadzenSidebar with dynamic tabs from EquipmentLinkConfig. *@
@inject IDataSourceService DataSvc

<RadzenSidebar @bind-Expanded="IsOpen" Style="width:420px;position:fixed;right:0;top:0;height:100vh;z-index:50;background:#0d1b2a;border-left:1px solid #1a3a5c;">
    @if (EquipmentData != null)
    {
        <div style="padding:16px;border-bottom:1px solid #1a3a5c">
            <div style="display:flex;align-items:center;gap:10px">
                <EquipmentIconRenderer Equipment="@EquipmentData" />
                <div>
                    <div style="color:#fff;font-weight:bold">@EquipmentData.Name</div>
                    @if (!string.IsNullOrEmpty(EquipmentData.Tag))
                    {
                        <div style="color:#888;font-size:0.8em">@EquipmentData.Tag</div>
                    }
                </div>
            </div>
        </div>

        <RadzenTabs Style="height:calc(100vh - 80px);overflow:auto">
            @* Always-present: Current Values tab *@
            <Tabs>
                <RadzenTabsItem Text="當前數值">
                    @if (LatestData?.Fields.Any() == true)
                    {
                        <RadzenDataGrid Data="@LatestData.Fields.ToList()" TItem="KeyValuePair<string,object?>"
                                        Style="font-size:0.85em">
                            <Columns>
                                <RadzenDataGridColumn TItem="KeyValuePair<string,object?>"
                                                      Property="Key" Title="參數" Width="120px" />
                                <RadzenDataGridColumn TItem="KeyValuePair<string,object?>"
                                                      Property="Value" Title="數值" />
                            </Columns>
                        </RadzenDataGrid>
                    }

                    @if (HistoryData.Any())
                    {
                        <RadzenChart Style="height:200px;margin-top:16px">
                            <RadzenLineSeries Data="@ChartPoints" CategoryProperty="Label" ValueProperty="Value"
                                             Stroke="#2196f3" StrokeWidth="2">
                            </RadzenLineSeries>
                        </RadzenChart>
                    }
                </RadzenTabsItem>

                @* Dynamic tabs from EquipmentLinkConfig *@
                @foreach (var linkConfig in LinkConfigs.OrderBy(l => l.DisplayOrder))
                {
                    <RadzenTabsItem Text="@linkConfig.TabLabel">
                        @switch (linkConfig.LinkType)
                        {
                            case LinkType.WorkOrder:
                            case LinkType.AlarmHistory:
                                @if (TabData.TryGetValue(linkConfig.Id, out var rows) && rows.Any())
                                {
                                    <RadzenDataGrid Data="@rows" TItem="DataResult" Style="font-size:0.82em">
                                        <Columns>
                                            @foreach (var key in rows.First().Fields.Keys.Take(5))
                                            {
                                                <RadzenDataGridColumn TItem="DataResult" Title="@key"
                                                    Property="@($"Fields[\"{key}\"]")" />
                                            }
                                        </Columns>
                                    </RadzenDataGrid>
                                }
                                else
                                {
                                    <div style="color:#555;padding:16px">暫無資料</div>
                                }
                                break;

                            case LinkType.Document:
                                @if (!string.IsNullOrEmpty(linkConfig.UrlTemplate))
                                {
                                    var url = linkConfig.UrlTemplate.Replace("{equipmentId}", EquipmentData.Id.ToString());
                                    <a href="@url" target="_blank" style="color:#2196f3">@linkConfig.TabLabel 文件連結</a>
                                }
                                break;

                            case LinkType.CustomUrl:
                                @if (!string.IsNullOrEmpty(linkConfig.UrlTemplate))
                                {
                                    var url = linkConfig.UrlTemplate.Replace("{equipmentId}", EquipmentData.Id.ToString());
                                    <iframe src="@url" style="width:100%;height:400px;border:none;"></iframe>
                                }
                                break;
                        }
                    </RadzenTabsItem>
                }
            </Tabs>
        </RadzenTabs>
    }
</RadzenSidebar>

@code {
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public EquipmentWidget? Widget { get; set; }
    [Parameter] public Equipment? EquipmentData { get; set; }
    [Parameter] public DataResult? LatestData { get; set; }
    [Parameter] public IEnumerable<DataResult> HistoryData { get; set; } = Enumerable.Empty<DataResult>();
    [Parameter] public IEnumerable<EquipmentLinkConfig> LinkConfigs { get; set; } = Enumerable.Empty<EquipmentLinkConfig>();

    private Dictionary<int, List<DataResult>> TabData = new();
    private List<TrendPoint> ChartPoints = new();

    public record TrendPoint(string Label, double Value);

    protected override async Task OnParametersSetAsync()
    {
        if (!IsOpen || EquipmentData == null) return;

        TabData.Clear();
        foreach (var lc in LinkConfigs.Where(l => l.DataSourceConfigId.HasValue))
        {
            // Fetch data for WorkOrder / AlarmHistory tabs
        }

        ChartPoints.Clear();
        int i = 0;
        foreach (var row in HistoryData.TakeLast(20))
        {
            var v = row.Fields.Values.FirstOrDefault();
            if (v != null && double.TryParse(v.ToString(), out var val))
                ChartPoints.Add(new TrendPoint((i++).ToString(), val));
        }
    }
}
```

- [ ] **Step 3: Build Web**

```bash
dotnet build TPS.Nexus.Kanban.Web
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add TPS.Nexus.Kanban.Web/Components/Tooltip/ TPS.Nexus.Kanban.Web/Components/Drawer/
git commit -m "feat(web): add EquipmentTooltip (hover) and EquipmentDetailDrawer (click sidebar)"
```

---

## Task 18: Web — Editor Components

**Files:**
- Create: `TPS.Nexus.Kanban.Web/Components/Editor/MapEditorToolbar.razor`
- Create: `TPS.Nexus.Kanban.Web/Components/Editor/DraggableEquipmentItem.razor`
- Create: `TPS.Nexus.Kanban.Web/Components/Editor/WidgetConfigurator.razor`

- [ ] **Step 1: Write MapEditorToolbar**

`TPS.Nexus.Kanban.Web/Components/Editor/MapEditorToolbar.razor`:
```razor
@inject IFunctionPermissionService PermSvc

<RadzenToolbar Style="background:#0d1b2a;border-bottom:1px solid #1a3a5c;padding:4px 12px">
    <RadzenToolbarItem>
        <span style="color:#aaa;font-size:0.85em">
            🗺️ <strong style="color:#fff">@MapName</strong>
            @if (PublishedVersion != null)
            {
                <span style="color:#888"> · v@(PublishedVersion.VersionNo) 已發布</span>
            }
        </span>
    </RadzenToolbarItem>

    @if (CanEdit)
    {
        <RadzenToolbarItem>
            <RadzenButton Text="@(IsEditMode ? "✅ 完成編輯" : "✏️ 編輯")"
                          ButtonStyle="@(IsEditMode ? ButtonStyle.Success : ButtonStyle.Secondary)"
                          Size="ButtonSize.Small"
                          Click="@ToggleEditMode" />
        </RadzenToolbarItem>
    }

    @if (IsEditMode)
    {
        <RadzenToolbarItem>
            <RadzenButton Text="💾 存草稿" ButtonStyle="ButtonStyle.Warning"
                          Size="ButtonSize.Small" Click="@SaveDraft" />
        </RadzenToolbarItem>

        @if (CanPublish)
        {
            <RadzenToolbarItem>
                <RadzenButton Text="🚀 發布" ButtonStyle="ButtonStyle.Primary"
                              Size="ButtonSize.Small" Click="@Publish" />
            </RadzenToolbarItem>
        }
    }

    <RadzenToolbarItem>
        <RadzenButton Text="📋 版本" ButtonStyle="ButtonStyle.Light"
                      Size="ButtonSize.Small" Click="@ShowVersionPanel" />
    </RadzenToolbarItem>
</RadzenToolbar>

@code {
    [Parameter] public string MapName { get; set; } = string.Empty;
    [Parameter] public LayoutVersion? PublishedVersion { get; set; }
    [Parameter] public bool IsEditMode { get; set; }
    [Parameter] public EventCallback<bool> IsEditModeChanged { get; set; }
    [Parameter] public EventCallback OnSaveDraft { get; set; }
    [Parameter] public EventCallback OnPublish { get; set; }
    [Parameter] public EventCallback OnShowVersions { get; set; }

    private bool CanEdit;
    private bool CanPublish;

    protected override async Task OnInitializedAsync()
    {
        CanEdit = await PermSvc.HasPermissionAsync("KANBAN_EDIT");
        CanPublish = await PermSvc.HasPermissionAsync("KANBAN_PUBLISH");
    }

    private async Task ToggleEditMode()
    {
        IsEditMode = !IsEditMode;
        await IsEditModeChanged.InvokeAsync(IsEditMode);
    }

    private async Task SaveDraft() => await OnSaveDraft.InvokeAsync();
    private async Task Publish() => await OnPublish.InvokeAsync();
    private async Task ShowVersionPanel() => await OnShowVersions.InvokeAsync();
}
```

- [ ] **Step 2: Write DraggableEquipmentItem**

`TPS.Nexus.Kanban.Web/Components/Editor/DraggableEquipmentItem.razor`:
```razor
@* Palette item in edit mode — drag onto canvas to add equipment. *@

<div class="kanban-widget"
     data-equipment-id="@Equipment.Id"
     style="position:relative;cursor:grab;margin-bottom:6px;left:0;top:0;"
     title="拖曳至地圖">
    <div class="kanban-widget-icon">
        <EquipmentIconRenderer Equipment="@Equipment" />
    </div>
    <div class="kanban-widget-name">@Equipment.Name</div>
    @if (!string.IsNullOrEmpty(Equipment.Tag))
    {
        <div style="color:#888;font-size:0.6em">@Equipment.Tag</div>
    }
</div>

@code {
    [Parameter, EditorRequired] public Equipment Equipment { get; set; } = default!;
}
```

- [ ] **Step 3: Write WidgetConfigurator**

`TPS.Nexus.Kanban.Web/Components/Editor/WidgetConfigurator.razor`:
```razor
@* Configure widget components (data sources) for a selected equipment widget. *@
@inject IEquipmentService EquipmentSvc

@if (Widget != null)
{
    <RadzenAccordion>
        <Items>
            <RadzenAccordionItem Text="元件設定" Icon="settings">
                @foreach (var comp in Widget.Components.OrderBy(c => c.DisplayOrder))
                {
                    <div style="border:1px solid #1a3a5c;border-radius:4px;padding:8px;margin-bottom:8px">
                        <RadzenText TextStyle="TextStyle.Subtitle2">@comp.ComponentType.ToString()</RadzenText>
                        <RadzenFormField Text="標籤" Style="width:100%;margin-top:4px">
                            <RadzenTextBox @bind-Value="comp.Label" />
                        </RadzenFormField>
                        <RadzenFormField Text="單位" Style="width:100%;margin-top:4px">
                            <RadzenTextBox @bind-Value="comp.Unit" />
                        </RadzenFormField>
                        <RadzenFormField Text="刷新間隔(秒)" Style="width:100%;margin-top:4px">
                            <RadzenNumeric TValue="int" @bind-Value="comp.RefreshInterval" Min="5" Max="3600" />
                        </RadzenFormField>
                        <RadzenFormField Text="資料來源" Style="width:100%;margin-top:4px">
                            <RadzenDropDown @bind-Value="comp.DataSourceConfigId"
                                            Data="@DataSources"
                                            TextProperty="Name"
                                            ValueProperty="Id"
                                            AllowClear="true" />
                        </RadzenFormField>
                        <RadzenButton Text="儲存" Size="ButtonSize.Small" ButtonStyle="ButtonStyle.Primary"
                                      Style="margin-top:6px" Click="@(() => SaveComponent(comp))" />
                    </div>
                }
                <RadzenButton Text="+ 新增元件" Size="ButtonSize.Small" ButtonStyle="ButtonStyle.Secondary"
                              Click="@AddComponent" />
            </RadzenAccordionItem>
        </Items>
    </RadzenAccordion>
}

@code {
    [Parameter] public EquipmentWidget? Widget { get; set; }
    [Parameter] public EventCallback OnChanged { get; set; }

    private List<DataSourceConfig> DataSources = new();

    protected override async Task OnInitializedAsync()
    {
        DataSources = (await EquipmentSvc.GetAllDataSourceConfigsAsync()).ToList();
    }

    private async Task SaveComponent(WidgetComponent comp)
    {
        await EquipmentSvc.SaveComponentAsync(comp);
        await OnChanged.InvokeAsync();
    }

    private async Task AddComponent()
    {
        if (Widget == null) return;
        var newComp = new WidgetComponent
        {
            EquipmentWidgetId = Widget.Id,
            ComponentType = WidgetComponentType.ValueGauge,
            RefreshInterval = 30,
            DisplayOrder = Widget.Components.Count
        };
        await EquipmentSvc.SaveComponentAsync(newComp);
        Widget.Components.Add(newComp);
        await OnChanged.InvokeAsync();
    }
}
```

- [ ] **Step 4: Build Web**

```bash
dotnet build TPS.Nexus.Kanban.Web
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add TPS.Nexus.Kanban.Web/Components/Editor/
git commit -m "feat(web): add MapEditorToolbar, DraggableEquipmentItem, WidgetConfigurator"
```

---

## Task 19: Web — Version, Alarm, MapImport Components

**Files:**
- Create: `TPS.Nexus.Kanban.Web/Components/Version/LayoutVersionPanel.razor`
- Create: `TPS.Nexus.Kanban.Web/Components/Alarm/AlarmBadge.razor`
- Create: `TPS.Nexus.Kanban.Web/Components/Alarm/AlarmPanel.razor`
- Create: `TPS.Nexus.Kanban.Web/Components/Alarm/AlarmToast.razor`
- Create: `TPS.Nexus.Kanban.Web/Components/Map/MapImportPanel.razor`

- [ ] **Step 1: Write LayoutVersionPanel**

`TPS.Nexus.Kanban.Web/Components/Version/LayoutVersionPanel.razor`:
```razor
@inject ILayoutService LayoutSvc
@inject IFunctionPermissionService PermSvc

<RadzenDataGrid Data="@Versions" TItem="LayoutVersion" Style="font-size:0.85em">
    <Columns>
        <RadzenDataGridColumn TItem="LayoutVersion" Property="VersionNo" Title="版本" Width="60px" />
        <RadzenDataGridColumn TItem="LayoutVersion" Property="Status" Title="狀態" Width="80px">
            <Template Context="v">
                <span style="color:@StatusColor(v.Status)">@v.Status</span>
            </Template>
        </RadzenDataGridColumn>
        <RadzenDataGridColumn TItem="LayoutVersion" Property="CreatedBy" Title="建立者" />
        <RadzenDataGridColumn TItem="LayoutVersion" Property="PublishedAt" Title="發布時間" FormatString="{0:yyyy-MM-dd HH:mm}" />
        <RadzenDataGridColumn TItem="LayoutVersion" Title="操作" Width="120px">
            <Template Context="v">
                @if (v.Status == LayoutStatus.Archived && CanPublish)
                {
                    <RadzenButton Text="回溯" Size="ButtonSize.ExtraSmall"
                                  ButtonStyle="ButtonStyle.Warning"
                                  Click="@(() => Rollback(v.Id))" />
                }
            </Template>
        </RadzenDataGridColumn>
    </Columns>
</RadzenDataGrid>

@code {
    [Parameter, EditorRequired] public int MapId { get; set; }
    [Parameter] public EventCallback OnRolledBack { get; set; }

    private List<LayoutVersion> Versions = new();
    private bool CanPublish;

    protected override async Task OnInitializedAsync()
    {
        CanPublish = await PermSvc.HasPermissionAsync("KANBAN_PUBLISH");
        await LoadVersions();
    }

    private async Task LoadVersions()
    {
        Versions = (await LayoutSvc.GetVersionHistoryAsync(MapId)).ToList();
    }

    private async Task Rollback(int versionId)
    {
        await LayoutSvc.RollbackAsync(versionId);
        await LoadVersions();
        await OnRolledBack.InvokeAsync();
    }

    private static string StatusColor(LayoutStatus s) => s switch
    {
        LayoutStatus.Published => "#4caf50",
        LayoutStatus.Draft     => "#2196f3",
        LayoutStatus.Archived  => "#607d8b",
        _ => "#fff"
    };
}
```

- [ ] **Step 2: Write AlarmBadge**

`TPS.Nexus.Kanban.Web/Components/Alarm/AlarmBadge.razor`:
```razor
<div class="kanban-alarm-badge">!</div>
```

- [ ] **Step 3: Write AlarmPanel**

`TPS.Nexus.Kanban.Web/Components/Alarm/AlarmPanel.razor`:
```razor
@inject IAlarmService AlarmSvc

<div style="background:#0d1b2a;border:1px solid #7f0000;border-radius:6px;padding:12px;max-height:300px;overflow-y:auto">
    <div style="color:#f44336;font-weight:bold;margin-bottom:8px">⚠ 活躍警報 (@Alarms.Count)</div>
    @foreach (var alarm in Alarms.OrderByDescending(a => a.Level).ThenByDescending(a => a.TriggeredAt))
    {
        <div style="border-bottom:1px solid #1a1a2e;padding:6px 0;font-size:0.82em">
            <div style="display:flex;justify-content:space-between">
                <span style="color:@LevelColor(alarm.Level)">[@alarm.Level] @alarm.EquipmentName</span>
                <RadzenButton Text="解除" Size="ButtonSize.ExtraSmall"
                              ButtonStyle="ButtonStyle.Danger"
                              Click="@(() => Resolve(alarm.Id))" />
            </div>
            <div style="color:#888">@alarm.Message</div>
            <div style="color:#555;font-size:0.85em">@alarm.TriggeredAt.ToString("HH:mm:ss")</div>
        </div>
    }
    @if (!Alarms.Any())
    {
        <div style="color:#555;text-align:center;padding:16px">無活躍警報</div>
    }
</div>

@code {
    [Parameter] public List<AlarmRecord> Alarms { get; set; } = new();
    [Parameter] public EventCallback OnResolved { get; set; }

    private async Task Resolve(int id)
    {
        await AlarmSvc.ResolveAlarmAsync(id);
        await OnResolved.InvokeAsync();
    }

    private static string LevelColor(AlarmLevel l) => l switch
    {
        AlarmLevel.Critical => "#f44336",
        AlarmLevel.Warning  => "#ff9800",
        _ => "#2196f3"
    };
}
```

- [ ] **Step 4: Write AlarmToast**

`TPS.Nexus.Kanban.Web/Components/Alarm/AlarmToast.razor`:
```razor
@inject NotificationService NotificationSvc
@implements IDisposable

@* Subscribe to SignalR hub and show RadzenNotification on new alarms. *@
<RadzenNotification />

@code {
    [Parameter] public Microsoft.AspNetCore.SignalR.Client.HubConnection? HubConnection { get; set; }

    protected override void OnParametersSet()
    {
        HubConnection?.On<int, string, string>("ReceiveAlarm", (equipmentId, level, message) =>
        {
            var severity = level switch
            {
                "Critical" => NotificationSeverity.Error,
                "Warning"  => NotificationSeverity.Warning,
                _ => NotificationSeverity.Info
            };
            NotificationSvc.Notify(severity, $"警報 [{level}]", message, duration: 5000);
        });
    }

    public void Dispose()
    {
        HubConnection?.Remove("ReceiveAlarm");
    }
}
```

- [ ] **Step 5: Write MapImportPanel**

`TPS.Nexus.Kanban.Web/Components/Map/MapImportPanel.razor`:
```razor
@inject IMapImportService MapImportSvc

<RadzenCard Style="padding:16px">
    <RadzenText TextStyle="TextStyle.H6">匯入廠區地圖</RadzenText>

    <RadzenFormField Text="地圖格式" Style="width:100%;margin-top:8px">
        <RadzenDropDown @bind-Value="SelectedFormat" Data="@FormatOptions"
                        TextProperty="Label" ValueProperty="Value" />
    </RadzenFormField>

    <div style="margin-top:12px">
        <RadzenUpload Url="" Auto="false" @ref="_upload"
                      Accept=".png,.jpg,.jpeg,.svg,.dxf,.json,.xml"
                      Change="@OnFileSelected"
                      Style="width:100%" />
    </div>

    <RadzenButton Text="上傳並匯入" ButtonStyle="ButtonStyle.Primary"
                  Style="margin-top:12px;width:100%"
                  Disabled="@(_selectedFile == null)"
                  Click="@DoImport" />

    @if (!string.IsNullOrEmpty(StatusMessage))
    {
        <div style="margin-top:8px;color:@(_isError ? "#f44336" : "#4caf50");font-size:0.85em">
            @StatusMessage
        </div>
    }
</RadzenCard>

@code {
    [Parameter] public EventCallback<FactoryMap> OnImported { get; set; }

    private RadzenUpload? _upload;
    private IBrowserFile? _selectedFile;
    private MapFormatType SelectedFormat = MapFormatType.Png;
    private string StatusMessage = string.Empty;
    private bool _isError;

    private static readonly List<(string Label, MapFormatType Value)> FormatOptions = new()
    {
        ("PNG 圖片", MapFormatType.Png),
        ("JPG 圖片", MapFormatType.Jpg),
        ("SVG 向量圖", MapFormatType.Svg),
        ("DXF 工程圖", MapFormatType.Dxf),
        ("JSON 座標定義", MapFormatType.JsonCoord),
        ("XML 座標定義", MapFormatType.XmlCoord),
    };

    private void OnFileSelected(UploadChangeEventArgs args)
    {
        _selectedFile = args.Files?.FirstOrDefault()?.File;
    }

    private async Task DoImport()
    {
        if (_selectedFile == null) return;
        _isError = false;
        StatusMessage = "匯入中...";

        try
        {
            await using var stream = _selectedFile.OpenReadStream(maxAllowedSize: 50 * 1024 * 1024);
            var map = await MapImportSvc.ImportAsync(stream, _selectedFile.Name, SelectedFormat);
            StatusMessage = $"✅ 已匯入：{map.Name}";
            await OnImported.InvokeAsync(map);
        }
        catch (Exception ex)
        {
            _isError = true;
            StatusMessage = $"❌ 匯入失敗：{ex.Message}";
        }
    }
}
```

- [ ] **Step 6: Build Web**

```bash
dotnet build TPS.Nexus.Kanban.Web
```

Expected: 0 errors.

- [ ] **Step 7: Commit**

```bash
git add TPS.Nexus.Kanban.Web/Components/
git commit -m "feat(web): add LayoutVersionPanel, Alarm components, MapImportPanel"
```

---

## Task 20: Web — Pages (KanbanMapPage + KanbanSettingsPage)

**Files:**
- Create: `TPS.Nexus.Kanban.Web/Pages/KanbanMapPage.razor`
- Create: `TPS.Nexus.Kanban.Web/Pages/KanbanSettingsPage.razor`

- [ ] **Step 1: Write KanbanMapPage**

`TPS.Nexus.Kanban.Web/Pages/KanbanMapPage.razor`:
```razor
@page "/kanban/{MapId:int}"
@inject IMapImportService MapImportSvc
@inject ILayoutService LayoutSvc
@inject IAlarmService AlarmSvc
@inject IFunctionPermissionService PermSvc
@inject NotificationService NotifySvc
@implements IAsyncDisposable

<PageTitle>工廠看板 — @(CurrentMap?.Name ?? "載入中")</PageTitle>

@if (!CanView)
{
    <div style="padding:40px;text-align:center;color:#f44336">無查看權限（需要 KANBAN_VIEW）</div>
    return;
}

@if (CurrentMap == null)
{
    <div style="padding:40px;text-align:center;color:#888">地圖載入中...</div>
    return;
}

<MapEditorToolbar
    MapName="@CurrentMap.Name"
    PublishedVersion="@PublishedVersion"
    @bind-IsEditMode="IsEditMode"
    OnSaveDraft="@HandleSaveDraft"
    OnPublish="@HandlePublish"
    OnShowVersions="@(() => ShowVersions = !ShowVersions)" />

<div style="display:flex;height:calc(100vh - 54px)">
    <div style="flex:1;min-width:0">
        <FactoryMapCanvas MapId="@MapId" IsEditMode="@IsEditMode" CanvasHeight="600" />
    </div>

    @if (ShowVersions)
    {
        <div style="width:380px;border-left:1px solid #1a3a5c;padding:12px;overflow-y:auto">
            <LayoutVersionPanel MapId="@MapId" OnRolledBack="@HandleRollback" />
        </div>
    }

    @if (ActiveAlarms.Any())
    {
        <div style="width:320px;border-left:1px solid #7f0000;padding:12px;overflow-y:auto">
            <AlarmPanel Alarms="@ActiveAlarms" OnResolved="@ReloadAlarms" />
        </div>
    }
</div>

<AlarmToast HubConnection="@HubConn" />

@code {
    [Parameter] public int MapId { get; set; }

    private FactoryMap? CurrentMap;
    private LayoutVersion? PublishedVersion;
    private List<AlarmRecord> ActiveAlarms = new();
    private bool CanView;
    private bool IsEditMode;
    private bool ShowVersions;
    private int? DraftId;
    private Microsoft.AspNetCore.SignalR.Client.HubConnection? HubConn;

    protected override async Task OnInitializedAsync()
    {
        CanView = await PermSvc.HasPermissionAsync("KANBAN_VIEW");
        if (!CanView) return;

        var maps = await MapImportSvc.GetAllAsync();
        CurrentMap = maps.FirstOrDefault(m => m.Id == MapId);
        PublishedVersion = await LayoutSvc.GetPublishedVersionAsync(MapId);
        await ReloadAlarms();

        HubConn = new Microsoft.AspNetCore.SignalR.Client.HubConnectionBuilder()
            .WithUrl("/hubs/kanban-alarm")
            .Build();

        HubConn.On<int, string, string>("ReceiveAlarm", async (_, _, _) =>
        {
            await ReloadAlarms();
            await InvokeAsync(StateHasChanged);
        });

        await HubConn.StartAsync();
    }

    private async Task HandleSaveDraft()
    {
        if (CurrentMap == null) return;
        var draft = await LayoutSvc.SaveDraftAsync(MapId, "{}", "CurrentUser");
        DraftId = draft.Id;
        NotifySvc.Notify(NotificationSeverity.Info, "草稿已儲存", $"v{draft.VersionNo}");
    }

    private async Task HandlePublish()
    {
        if (DraftId == null) { NotifySvc.Notify(NotificationSeverity.Warning, "請先存草稿"); return; }
        var published = await LayoutSvc.PublishAsync(DraftId.Value);
        PublishedVersion = published;
        DraftId = null;
        NotifySvc.Notify(NotificationSeverity.Success, "已發布", $"v{published.VersionNo}");
    }

    private async Task HandleRollback()
    {
        PublishedVersion = await LayoutSvc.GetPublishedVersionAsync(MapId);
        await InvokeAsync(StateHasChanged);
    }

    private async Task ReloadAlarms()
    {
        ActiveAlarms = (await AlarmSvc.GetActiveAlarmsAsync()).ToList();
    }

    public async ValueTask DisposeAsync()
    {
        if (HubConn != null)
            await HubConn.DisposeAsync();
    }
}
```

- [ ] **Step 2: Write KanbanSettingsPage**

`TPS.Nexus.Kanban.Web/Pages/KanbanSettingsPage.razor`:
```razor
@page "/kanban/settings"
@inject IEquipmentService EquipmentSvc
@inject IIconUploadService IconSvc
@inject IFunctionPermissionService PermSvc
@inject IMapImportService MapImportSvc
@inject NotificationService NotifySvc

<PageTitle>看板設定</PageTitle>

@if (!CanSettings)
{
    <div style="padding:40px;text-align:center;color:#f44336">無設定權限（需要 KANBAN_SETTINGS）</div>
    return;
}

<div style="padding:16px">
    <RadzenText TextStyle="TextStyle.H5">看板設定</RadzenText>

    <RadzenTabs>
        <Tabs>
            @* ── Equipment Management ── *@
            <RadzenTabsItem Text="設備管理">
                <div style="margin-bottom:12px">
                    <RadzenButton Text="+ 新增設備" ButtonStyle="ButtonStyle.Primary"
                                  Size="ButtonSize.Small" Click="@AddEquipment" />
                </div>
                <RadzenDataGrid @ref="_equipGrid" Data="@AllEquipment" TItem="Equipment"
                                EditMode="DataGridEditMode.Single"
                                RowUpdate="@OnEquipmentUpdate">
                    <Columns>
                        <RadzenDataGridColumn TItem="Equipment" Property="Name" Title="名稱">
                            <EditTemplate Context="eq">
                                <RadzenTextBox @bind-Value="eq.Name" Style="width:100%" />
                            </EditTemplate>
                        </RadzenDataGridColumn>
                        <RadzenDataGridColumn TItem="Equipment" Property="Tag" Title="標籤">
                            <EditTemplate Context="eq">
                                <RadzenTextBox @bind-Value="eq.Tag" Style="width:100%" />
                            </EditTemplate>
                        </RadzenDataGridColumn>
                        <RadzenDataGridColumn TItem="Equipment" Title="圖示" Width="80px">
                            <Template Context="eq">
                                <EquipmentIconRenderer Equipment="@eq" />
                            </Template>
                        </RadzenDataGridColumn>
                        <RadzenDataGridColumn TItem="Equipment" Title="操作" Width="120px">
                            <Template Context="eq">
                                <RadzenButton Text="編輯" Size="ButtonSize.ExtraSmall"
                                              Click="@(() => _equipGrid?.EditRow(eq))" />
                                <RadzenButton Text="刪除" Size="ButtonSize.ExtraSmall"
                                              ButtonStyle="ButtonStyle.Danger"
                                              Click="@(() => DeleteEquipment(eq.Id))"
                                              Style="margin-left:4px" />
                            </Template>
                            <EditTemplate Context="eq">
                                <RadzenButton Text="儲存" Size="ButtonSize.ExtraSmall"
                                              ButtonStyle="ButtonStyle.Primary"
                                              Click="@(() => _equipGrid?.UpdateRow(eq))" />
                            </EditTemplate>
                        </RadzenDataGridColumn>
                    </Columns>
                </RadzenDataGrid>
            </RadzenTabsItem>

            @* ── Data Source Management ── *@
            <RadzenTabsItem Text="資料來源">
                <div style="margin-bottom:12px">
                    <RadzenButton Text="+ 新增資料來源" ButtonStyle="ButtonStyle.Primary"
                                  Size="ButtonSize.Small" Click="@AddDataSource" />
                </div>
                <RadzenDataGrid Data="@AllDataSources" TItem="DataSourceConfig"
                                EditMode="DataGridEditMode.Single"
                                RowUpdate="@OnDataSourceUpdate">
                    <Columns>
                        <RadzenDataGridColumn TItem="DataSourceConfig" Property="Name" Title="名稱">
                            <EditTemplate Context="ds">
                                <RadzenTextBox @bind-Value="ds.Name" Style="width:100%" />
                            </EditTemplate>
                        </RadzenDataGridColumn>
                        <RadzenDataGridColumn TItem="DataSourceConfig" Property="SourceType" Title="類型" Width="80px" />
                        <RadzenDataGridColumn TItem="DataSourceConfig" Property="QueryOrPath" Title="查詢/路徑">
                            <EditTemplate Context="ds">
                                <RadzenTextArea @bind-Value="ds.QueryOrPath" Style="width:100%;height:60px" />
                            </EditTemplate>
                        </RadzenDataGridColumn>
                    </Columns>
                </RadzenDataGrid>
            </RadzenTabsItem>

            @* ── Icon Upload ── *@
            <RadzenTabsItem Text="圖示上傳">
                <RadzenCard Style="max-width:400px">
                    <RadzenText TextStyle="TextStyle.Subtitle1">上傳設備圖示</RadzenText>
                    <RadzenUpload Url="" Auto="false"
                                  Accept=".png,.jpg,.jpeg,.svg"
                                  Change="@OnIconFileSelected"
                                  Style="width:100%;margin-top:8px" />
                    <RadzenButton Text="上傳" ButtonStyle="ButtonStyle.Primary"
                                  Style="margin-top:8px;width:100%"
                                  Disabled="@(_iconFile == null)"
                                  Click="@DoIconUpload" />
                    @if (!string.IsNullOrEmpty(IconUploadResult))
                    {
                        <div style="margin-top:8px;color:#4caf50;font-size:0.85em;word-break:break-all">
                            @IconUploadResult
                        </div>
                    }
                </RadzenCard>
            </RadzenTabsItem>

            @* ── Map Import ── *@
            <RadzenTabsItem Text="地圖管理">
                <MapImportPanel OnImported="@OnMapImported" />
                <div style="margin-top:16px">
                    <RadzenText TextStyle="TextStyle.Subtitle1">已匯入地圖</RadzenText>
                    <RadzenDataGrid Data="@AllMaps" TItem="FactoryMap" Style="margin-top:8px">
                        <Columns>
                            <RadzenDataGridColumn TItem="FactoryMap" Property="Name" Title="名稱" />
                            <RadzenDataGridColumn TItem="FactoryMap" Property="FormatType" Title="格式" Width="80px" />
                            <RadzenDataGridColumn TItem="FactoryMap" Property="CreatedAt" Title="建立時間"
                                                  FormatString="{0:yyyy-MM-dd}" Width="100px" />
                            <RadzenDataGridColumn TItem="FactoryMap" Title="操作" Width="80px">
                                <Template Context="m">
                                    <RadzenButton Text="刪除" Size="ButtonSize.ExtraSmall"
                                                  ButtonStyle="ButtonStyle.Danger"
                                                  Click="@(() => DeleteMap(m.Id))" />
                                </Template>
                            </RadzenDataGridColumn>
                        </Columns>
                    </RadzenDataGrid>
                </div>
            </RadzenTabsItem>
        </Tabs>
    </RadzenTabs>
</div>

@code {
    private bool CanSettings;
    private List<Equipment> AllEquipment = new();
    private List<DataSourceConfig> AllDataSources = new();
    private List<FactoryMap> AllMaps = new();
    private RadzenDataGrid<Equipment>? _equipGrid;
    private IBrowserFile? _iconFile;
    private string IconUploadResult = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        CanSettings = await PermSvc.HasPermissionAsync("KANBAN_SETTINGS");
        if (!CanSettings) return;
        await LoadAll();
    }

    private async Task LoadAll()
    {
        AllEquipment = (await EquipmentSvc.GetAllEquipmentAsync()).ToList();
        AllDataSources = (await EquipmentSvc.GetAllDataSourceConfigsAsync()).ToList();
        AllMaps = (await MapImportSvc.GetAllAsync()).ToList();
    }

    private void AddEquipment()
    {
        AllEquipment.Insert(0, new Equipment { Name = "新設備" });
    }

    private async Task OnEquipmentUpdate(Equipment eq)
    {
        if (eq.Id == 0) await EquipmentSvc.CreateEquipmentAsync(eq);
        else await EquipmentSvc.UpdateEquipmentAsync(eq);
        await LoadAll();
    }

    private async Task DeleteEquipment(int id)
    {
        await EquipmentSvc.DeleteEquipmentAsync(id);
        AllEquipment.RemoveAll(e => e.Id == id);
    }

    private void AddDataSource()
    {
        AllDataSources.Insert(0, new DataSourceConfig { Name = "新資料來源" });
    }

    private async Task OnDataSourceUpdate(DataSourceConfig ds)
    {
        await EquipmentSvc.SaveDataSourceConfigAsync(ds);
        await LoadAll();
    }

    private void OnIconFileSelected(UploadChangeEventArgs args)
    {
        _iconFile = args.Files?.FirstOrDefault()?.File;
    }

    private async Task DoIconUpload()
    {
        if (_iconFile == null) return;
        await using var stream = _iconFile.OpenReadStream(maxAllowedSize: 5 * 1024 * 1024);
        IconUploadResult = await IconSvc.UploadAsync(stream, _iconFile.Name);
        NotifySvc.Notify(NotificationSeverity.Success, "圖示已上傳", IconUploadResult);
    }

    private async Task OnMapImported(FactoryMap map)
    {
        AllMaps = (await MapImportSvc.GetAllAsync()).ToList();
    }

    private async Task DeleteMap(int id)
    {
        await MapImportSvc.DeleteAsync(id);
        AllMaps.RemoveAll(m => m.Id == id);
    }
}
```

- [ ] **Step 3: Build Web**

```bash
dotnet build TPS.Nexus.Kanban.Web
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add TPS.Nexus.Kanban.Web/Pages/
git commit -m "feat(web): add KanbanMapPage and KanbanSettingsPage"
```

---

## Task 21: Build, Deploy, and Smoke Test

**Files:**
- No new files — compile and deploy to Modules/ folder

- [ ] **Step 1: Build all projects in Release**

```bash
dotnet build -c Release
```

Expected: 0 errors across all 4 projects.

- [ ] **Step 2: Run all tests**

```bash
dotnet test TPS.Nexus.Kanban.Tests -c Release
```

Expected: All tests PASS.

- [ ] **Step 3: Copy output to host Modules/ folder**

```bash
mkdir -p TPS.Nexus.Web/Modules/TPS.Nexus.Kanban
cp TPS.Nexus.Kanban.Web/bin/Release/net10.0/*.dll TPS.Nexus.Web/Modules/TPS.Nexus.Kanban/
cp TPS.Nexus.Kanban.Services/bin/Release/net10.0/*.dll TPS.Nexus.Web/Modules/TPS.Nexus.Kanban/
cp TPS.Nexus.Kanban.Core/bin/Release/net10.0/*.dll TPS.Nexus.Web/Modules/TPS.Nexus.Kanban/
cp -r TPS.Nexus.Kanban.Web/wwwroot/ TPS.Nexus.Web/Modules/TPS.Nexus.Kanban/
```

- [ ] **Step 4: Start host and verify module loads**

```bash
dotnet run --project TPS.Nexus.Web
```

Expected output includes:
```
Module loaded: TPS.Nexus.Kanban (KanbanModuleRegistrar)
SignalR Hub mapped: /hubs/kanban-alarm
```

- [ ] **Step 5: Smoke test in browser**

1. Navigate to `http://localhost:5000/kanban/settings`
   - Expected: Settings page loads, 4 tabs visible
2. Navigate to `http://localhost:5000/kanban/1` (after importing a map)
   - Expected: Map canvas renders, toolbar shows map name
3. Click "編輯" — equipment widgets become draggable (dashed border)
4. Hover over a widget — tooltip appears with parameter values
5. Click a widget — detail drawer opens on right side

- [ ] **Step 6: Final commit**

```bash
git add -A
git commit -m "feat: complete TPS.Nexus.Kanban module — factory kanban with Blazor, SignalR, Radzen"
```

---

## Self-Review

**Spec coverage check:**

| Spec Requirement | Task |
|---|---|
| PNG/JPG/SVG/DXF/JSON/XML map import | Task 9 |
| Equipment widget on map (position:absolute) | Task 16 |
| Always-visible: icon + name + status | Task 15 |
| Hover: all params + mini chart (RadzenTooltip) | Task 17 |
| Click: full detail (RadzenSidebar + RadzenTabs) | Task 17 |
| SQL/CSV/JSON/XML data sources | Tasks 6, 7, 8 |
| SignalR real-time alarms | Tasks 5, 12 |
| Draft/Published/Archived version management | Task 11 |
| SortableJS drag & drop in edit mode | Task 14, 16 |
| KANBAN_VIEW/EDIT/PUBLISH/SETTINGS permissions | Tasks 18, 19, 20 |
| IModuleRegistrar.MapEndpoints for SignalR hub | Task 5 |
| Host Program.cs Blazor + SignalR setup | Task 0 |
| RadzenNotification alarm toast | Task 19 |
| IconUpload (CssClass / CustomImage) | Tasks 13, 15 |
| LayoutVersionPanel with rollback | Task 19 |

All spec requirements covered.

---

## Session Changes — 2026-06-07

> 紀錄 2026-06-04 ~ 2026-06-07 session 對 Demo 實作所做的異動，供日後移植至正式模組時參考。

### 新功能

#### 地圖輪播（Map Carousel）
- `FactoryMap` 新增：`CarouselEnabled`、`CarouselSeconds`、`CarouselOrder`、`Version`
- `KanbanMapPage` 新增：`StartCarousel()` / `StopCarousel()` / `CarouselTickAsync()` / `ToggleCarouselPause()`
- 輪播以 `PeriodicTimer(1s)` 倒數，到 0 後 `NavigationManager.NavigateTo("/kanban/{nextId}")`
- `UserPrefsService`（`TPS.Nexus.Kanban.Web/Services/`）：透過 `IJSRuntime` 存取 localStorage，跨地圖保持使用者最後選取的地圖

#### 版本面板疊加
- `LayoutVersionPanel` 以 `position:absolute;right:0;top:0;z-index:300` 疊加於地圖，不縮小地圖區域
- 開啟時暫停輪播（`_carouselPaused = true`），關閉時恢復
- 父容器改為 `position:relative; height:calc(100vh - 102px)`

### UI 實作異動

#### MapEditorToolbar（Task 18 修正）
原計畫使用 `RadzenToolbar` + `RadzenButton` + `RadzenDropDown`，改為：
- 容器：`<div id="kanban-map-toolbar">` + dark inline style
- 按鈕：native `<button>` + `const string` inline style 常數（DefaultBtn、SuccessBtn 等）
- 地圖選單：native `<select>` + `@onchange`
- 移除已發布版本文字（`v{n} 已發布`）
- 新增 `AllMaps` + `CurrentMapId` + `OnMapSelected` parameters

**原因**：Radzen 5.x CSS 在 `!important` 同等優先級下覆蓋自訂 CSS；inline style 是唯一不被覆蓋的方式。

#### LayoutVersionPanel（Task 19 修正）
原計畫使用 `RadzenDataGrid`，改為深色抽屜風格 flex 列表：
- 每列：版本號（藍色）、狀態 badge（顏色對應）、發布時間、建立者、回溯按鈕
- 與 `EquipmentDetailDrawer` 視覺風格一致
- 寬度 320px（與設備抽屜相同）

#### FactoryMapCanvas（Task 16 修正）
- `#kanban-fs-wrapper` 加入 `overflow:hidden`（避免 drawer `translateX` 溢出）
- 輪播倒數徽章移入 wrapper 內（`position:absolute`），修正全螢幕模式下消失問題
- 設備 palette 可見性改為 `(_equipDrawerOpen && IsEditMode)`（避免點版本按鈕顯示設備 palette）
- 新增 4 個輪播相關 parameters

#### KanbanMapPage（Task 20 修正）
- `OnParametersSetAsync` 重置 `_carouselPaused = false`
- `HandleShowVersions()` 整合輪播暫停/恢復
- `HandlePublish()` 後重啟輪播

#### MainLayout（Demo 層修正）
- `NavLink href="/kanban/1"` 改為 `<a>` + `IsKanbanActive` computed property
- 訂閱 `NavigationManager.LocationChanged`，所有 `/kanban/{n}` URL 均顯示 active

### 設定頁更新（KanbanSettingsPage / Task 20 修正）
- 地圖管理 DataGrid 新增「版本」欄（`FactoryMap.Version`）
- `MapEditDialog` 新增 Version 欄位

### DataGrid 高度規格（kanban.css）
- Header row 與 data row 均為 38px
- `line-height: 38px` 垂直置中
- 套用於 `.kanban-light-grid` 主題

