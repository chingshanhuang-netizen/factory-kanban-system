# TPS.Nexus 工廠看板系統模組 — 設計規格

**日期**: 2026-06-01（更新：2026-06-02）
**版本**: 1.2
**狀態**: 已核准

---

## 異動記錄

| 版本 | 日期 | 異動內容 |
|---|---|---|
| 1.0 | 2026-06-01 | 初版 |
| 1.1 | 2026-06-01 | Equipment 位置欄位移至 EquipmentWidget |
| 1.2 | 2026-06-02 | 宿主確認為 ASP.NET Core MVC；UI 庫由 DevExpress 改為 Radzen Blazor + SortableJS；資料庫確認為 MySQL；模組目錄結構更新 |

---

## 1. 專案背景與目標

開發一個工廠看板模組，整合至 TPS.Nexus 系統，讓操作員與管理者能在廠區樓層圖上即時掌握設備運行狀態，並透過多種資料來源（SQL、CSV、JSON、XML）驅動看板內容。

---

## 2. 技術環境

| 項目 | 值 |
|---|---|
| Framework | .NET 10.0 |
| 模組專案類型 | Razor Class Library（Blazor 元件） |
| 宿主 | TPS.Nexus.Web.exe（ASP.NET Core MVC + Blazor Server 並存） |
| 核心 DLL | TPS.Nexus.Core.dll |
| UI 元件庫 | **Radzen Blazor**（免費）+ **SortableJS**（拖拉） |
| 即時通訊 | SignalR（需明確加入宿主） |
| 資料庫 | **MySQL**（透過 MySqlConnectionFactory） |
| 日誌 | Serilog |

### TPS.Nexus.Core.dll 依賴介面

- `IDbConnectionFactory` — MySQL 連線抽象
- `IFunctionPermissionService` — 功能權限控制
- `IModuleRegistrar` — 模組向宿主系統註冊

---

## 3. 架構設計

### 3.1 專案結構（三層分離）

```
Solution/
├── TPS.Nexus.Kanban.Core        ← Class Library：Models、Interfaces、Enums
├── TPS.Nexus.Kanban.Services    ← Class Library：業務邏輯、資料適配器、SignalR Hub
└── TPS.Nexus.Kanban.Web         ← Razor Class Library（Blazor）：元件、頁面、靜態資源
```

相依方向：`Web → Services → Core → TPS.Nexus.Core.dll`

### 3.2 模組目錄結構（部署後）

```
Modules/
└── TPS.Nexus.Kanban/
    ├── TPS.Nexus.Kanban.Web.dll       ← 含 Blazor 元件（編譯後）
    ├── TPS.Nexus.Kanban.Services.dll
    ├── TPS.Nexus.Kanban.Core.dll
    └── wwwroot/                        ← 靜態資源（對應 /module-assets/TPS.Nexus.Kanban/）
        ├── js/
        │   └── sortable.min.js         ← SortableJS
        ├── css/
        │   └── kanban.css
        └── images/
            └── (設備圖示等)
```

### 3.3 宿主 Program.cs 必要修改

在現有 `Program.cs` 基礎上新增三處，使 MVC 與 Blazor 並存：

```csharp
// ① 加入 Blazor Server 與 SignalR 服務（在 AddControllersWithViews 附近）
builder.Services.AddServerSideBlazor();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSignalR();

// ② 在模組載入迴圈結束後加入（現有程式碼之後）
foreach (var module in modules)
{
    module.Registrar.RegisterServices(builder.Services, builder.Configuration);
}

// ③ 在 app.MapControllerRoute 之前加入
app.MapBlazorHub();
foreach (var module in modules)
{
    module.Registrar.MapEndpoints(app);  // 掛載各模組 SignalR Hub
}
```

### 3.4 IModuleRegistrar 介面新增方法

```csharp
public interface IModuleRegistrar
{
    void RegisterServices(IServiceCollection services, IConfiguration configuration);
    void MapEndpoints(IEndpointRouteBuilder endpoints); // ← 新增（其他模組空實作）
}
```

### 3.5 模組註冊流程

```
TPS.Nexus.Web.exe 啟動
→ 掃描並載入 Modules/TPS.Nexus.Kanban/TPS.Nexus.Kanban.Web.dll
→ 執行 KanbanModuleRegistrar : IModuleRegistrar
   RegisterServices()
   → 註冊所有 Service 層介面至 DI
   MapEndpoints()
   → endpoints.MapHub<KanbanAlarmHub>("/hubs/kanban-alarm")
→ 靜態資源掛載至 /module-assets/TPS.Nexus.Kanban/
→ Blazor 元件路由：/kanban/{mapId}、/kanban/settings
```

### 3.6 資料流

**一般數值（定時輪詢）**
```
Blazor PeriodicTimer 觸發 → IDataSourceService.FetchAsync()
→ MySQL / CSV / JSON / XML 適配器
→ InvokeAsync(StateHasChanged) → 畫面局部更新
```

**警報（即時推送）**
```
AlarmService.EvaluateAsync() 偵測條件觸發
→ IHubContext<KanbanAlarmHub>.Clients.All.SendAsync("ReceiveAlarm", ...)
→ SignalR 推送至所有連線用戶端
→ Blazor 元件接收後 StateHasChanged()
```

**佈局版本**
```
編輯模式拖拉（SortableJS 回呼） → ILayoutService.SaveDraftAsync()
→ 管理者審核後 ILayoutService.PublishAsync()
→ 建立新版本（舊版本 → Archived，可回溯）
```

---

## 4. Core 層

### 4.1 資料模型

#### FactoryMap — 廠區地圖
```csharp
Id, Name, FormatType(MapFormatType), FilePath, ThumbnailPath, CreatedAt
```

#### LayoutVersion — 佈局版本
```csharp
Id, FactoryMapId, VersionNo, Status(LayoutStatus)
CreatedBy, PublishedAt, LayoutJson
```

#### Equipment — 設備（基本資料，跨版本共用）
```csharp
Id, Name, Tag, Description
IconType(IconType), IconValue
// CssClass: "fa-solid fa-gear" | CustomImage: "/module-assets/TPS.Nexus.Kanban/images/cnc.png"
```

#### EquipmentWidget — 設備在特定版本的看板設定（含位置）
```csharp
Id, EquipmentId, LayoutVersionId
PositionX, PositionY, Width, Height   // 每個版本可有不同擺放位置
Components: List<WidgetComponent>
```

#### WidgetComponent — 可組合看板元件
```csharp
Id, ComponentType(WidgetComponentType), DataSourceConfigId
Label, Unit, RefreshInterval(秒), DisplayOrder, ConfigJson
```

#### DataSourceConfig — 資料來源設定
```csharp
Id, Name, SourceType(DataSourceType)
ConnectionString/FilePath, QueryOrPath, Parameters
```

#### AlarmRule — 警報規則
```csharp
Id, EquipmentId, DataSourceConfigId
Condition, Threshold, AlarmLevel(AlarmLevel)
```

#### EquipmentLinkConfig — 設備資訊連結設定
```csharp
Id, EquipmentId
LinkType(LinkType), TabLabel, UrlTemplate, DataSourceConfigId(nullable), DisplayOrder
```
> `UrlTemplate` 支援 `{equipmentId}` 參數替換，例如 `/workorders?eq={equipmentId}`

### 4.2 列舉型別

| Enum | 值 |
|---|---|
| `MapFormatType` | Png, Jpg, Svg, Dxf, JsonCoord, XmlCoord |
| `DataSourceType` | Sql, Csv, Json, Xml |
| `WidgetComponentType` | StatusIndicator, ValueGauge, TrendChart |
| `LayoutStatus` | Draft, Published, Archived |
| `AlarmLevel` | Info, Warning, Critical |
| `IconType` | CssClass, CustomImage |
| `LinkType` | WorkOrder, AlarmHistory, Document, CustomUrl |

### 4.3 服務介面

```csharp
IMapImportService
  Task<FactoryMap> ImportAsync(Stream file, MapFormatType format)

IDataSourceService
  Task<DataResult> FetchAsync(DataSourceConfig config)
  Task<IEnumerable<DataResult>> FetchHistoryAsync(config, from, to)

ILayoutService
  Task<LayoutVersion> SaveDraftAsync(layoutData)
  Task<LayoutVersion> PublishAsync(draftId)
  Task<IEnumerable<LayoutVersion>> GetVersionHistoryAsync(mapId)
  Task RollbackAsync(versionId)

IEquipmentService
  // Equipment、EquipmentWidget、WidgetComponent 的 CRUD
  Task<IEnumerable<EquipmentLinkConfig>> GetLinkConfigsAsync(equipmentId)
  Task SaveLinkConfigAsync(EquipmentLinkConfig config)
  Task DeleteLinkConfigAsync(id)

IAlarmService
  Task EvaluateAsync(equipmentId, latestData)
  Task<IEnumerable<AlarmRecord>> GetActiveAlarmsAsync()

IIconUploadService
  Task<string> UploadAsync(Stream file, string fileName)  // 儲存至 wwwroot/images/
  Task DeleteAsync(string filePath)
```

---

## 5. Service 層

### 5.1 目錄結構

```
TPS.Nexus.Kanban.Services/
├── Map/
│   ├── MapImportService.cs          : IMapImportService
│   ├── ImageMapHandler.cs           // PNG / JPG
│   ├── SvgMapParser.cs              // SVG
│   ├── DxfMapParser.cs              // DXF（依賴 netDxf，轉換為 SVG）
│   └── JsonXmlCoordParser.cs        // JSON / XML 座標定義
├── DataSource/
│   ├── DataSourceService.cs         : IDataSourceService
│   ├── SqlDataAdapter.cs            // IDbConnectionFactory（MySQL），參數化查詢
│   ├── CsvDataAdapter.cs            // CsvHelper
│   ├── JsonDataAdapter.cs           // System.Text.Json + JSONPath
│   └── XmlDataAdapter.cs            // System.Xml + XPath
├── Layout/
│   └── LayoutService.cs             : ILayoutService
├── Equipment/
│   └── EquipmentService.cs          : IEquipmentService
├── Alarm/
│   └── AlarmService.cs              : IAlarmService（注入 IHubContext<KanbanAlarmHub>）
├── Icon/
│   └── IconUploadService.cs         : IIconUploadService
├── Hubs/
│   └── KanbanAlarmHub.cs            : Hub（SignalR）
│       SendAlarmAsync(equipmentId, alarmLevel, message)
│       客戶端監聽事件："ReceiveAlarm"
└── KanbanModuleRegistrar.cs         : IModuleRegistrar
    RegisterServices() → 所有 Service DI 註冊
    MapEndpoints()     → endpoints.MapHub<KanbanAlarmHub>("/hubs/kanban-alarm")
```

### 5.2 NuGet 套件依賴

| 套件 | 用途 |
|---|---|
| `Radzen.Blazor` | UI 元件庫（免費） |
| `netDxf` | DXF 格式解析與轉換 |
| `CsvHelper` | CSV 解析，支援自訂分隔符 |
| `Microsoft.AspNetCore.SignalR` | 即時警報推送 |
| `System.Text.Json` | JSON 解析（內建） |
| `System.Xml` | XML 解析（內建） |

### 5.3 JavaScript 套件（wwwroot/js/）

| 套件 | 用途 | 載入路徑 |
|---|---|---|
| `sortable.min.js` | 設備拖拉放置（編輯模式） | `/module-assets/TPS.Nexus.Kanban/js/sortable.min.js` |
| `signalr.min.js` | SignalR 用戶端 | ASP.NET Core 內建 `/_blazor` |

---

## 6. View 層（RCL）

### 6.1 FactoryMapCanvas 圖層架構

```
<div style="position:relative">                         ← 座標基準容器
  ① <MapLayer />                                        ← 底層：廠區樓層圖
     PNG/JPG → <img>；SVG → inline <svg>；DXF → 轉換後 <svg>
  ② @foreach widget → <EquipmentWidgetPanel />          ← 設備層（position:absolute）
     style="left:@widget.PositionX px; top:@widget.PositionY px"
  ③ <EquipmentTooltip />                                ← 互動層（最上層）
     <EquipmentDetailDrawer />
</div>
```

### 6.2 設備互動：三層資訊揭露

| 層級 | 觸發 | 顯示內容 | Radzen 元件 |
|---|---|---|---|
| ① 地圖常駐 | 永遠可見 | 圖示 + 設備名稱 + 狀態燈號 | — |
| ② Hover 快覽 | 滑鼠停留 | 所有參數數值 + 迷你趨勢圖 | `RadzenTooltip` + `RadzenChart`（小型） |
| ③ Click 詳情 | 點擊設備 | 工單、警報原因、完整圖表、連結 | `RadzenSidebar` + `RadzenTabs` + `RadzenDataGrid` + `RadzenChart` |

**EquipmentWidgetPanel（地圖常駐，精簡）**
- `EquipmentIconRenderer` — CssClass 渲染 `<i>`；CustomImage 渲染 `<img>`
- 設備名稱（文字）
- `StatusIndicator` — 綠色/灰色/橘色/紅色閃爍，對應 運行/停機/待機/警報

### 6.3 EquipmentDetailDrawer 頁籤

依 `EquipmentLinkConfig` 動態產生，使用 `RadzenTabs`：

| LinkType | 呈現方式 |
|---|---|
| WorkOrder | `RadzenDataGrid` 工單清單，含行內連結 |
| AlarmHistory | `RadzenDataGrid` 警報歷史 + `RadzenChart` 趨勢 |
| Document | 文件連結清單 |
| CustomUrl | iframe 或外部連結按鈕 |

### 6.4 頁面（Pages/）

| 頁面 | 路由 | 說明 |
|---|---|---|
| `KanbanMapPage.razor` | `/kanban/{mapId}` | 主看板頁：地圖畫布 + 工具列 + 警報面板 |
| `KanbanSettingsPage.razor` | `/kanban/settings` | 設定：設備管理、資料來源、圖示上傳 |

### 6.5 元件清單（Components/）

```
Map/
  FactoryMapCanvas.razor    — 地圖底圖 + 設備疊層容器
  MapImportPanel.razor      — 地圖上傳（RadzenUpload + 格式選擇）

Widget/
  EquipmentWidgetPanel.razor — 地圖常駐精簡看板（圖示+名稱+狀態）
  StatusIndicator.razor      — 狀態燈（運行/停機/待機/警報）
  EquipmentIconRenderer.razor — CssClass / CustomImage 圖示渲染

Tooltip/
  EquipmentTooltip.razor    — RadzenTooltip：Hover 顯示參數值 + 小型 RadzenChart

Drawer/
  EquipmentDetailDrawer.razor — RadzenSidebar：Click 展開詳細側欄
                                RadzenTabs + RadzenDataGrid + RadzenChart

Editor/
  MapEditorToolbar.razor    — RadzenToolbar：編輯/檢視切換、草稿/發布
  DraggableEquipmentItem.razor — SortableJS 驅動的可拖拉設備節點
  WidgetConfigurator.razor  — RadzenAccordion：設定元件組合與資料源

Version/
  LayoutVersionPanel.razor  — RadzenDataGrid：版本清單（發布/回溯操作）

Alarm/
  AlarmBadge.razor          — 設備角標（有警報時顯示）
  AlarmPanel.razor          — 側欄警報清單
  AlarmToast.razor          — RadzenNotification：新警報即時通知
```

### 6.6 Radzen 元件對應

| 功能 | Radzen 元件 | 備註 |
|---|---|---|
| 趨勢圖表（Drawer） | `RadzenChart` | 折線/柱狀/面積 |
| Hover 快覽圖（Tooltip） | `RadzenChart`（小尺寸） | 嵌入 Tooltip 內 |
| Hover 快覽面板 | `RadzenTooltip` | |
| 詳情側欄 | `RadzenSidebar` | |
| 警報通知 | `RadzenNotification` | |
| 資料清單（工單/版本） | `RadzenDataGrid` | |
| 檔案上傳 | `RadzenUpload` | |
| 工具列 | `RadzenToolbar` | |
| 頁籤 | `RadzenTabs` | |
| 右鍵選單 | `RadzenContextMenu` | |
| 元件設定面板 | `RadzenAccordion` | |
| 設定表單 | `RadzenTemplateForm` + `RadzenTextBox` | |
| **拖拉放置** | **SortableJS**（JS 套件） | Blazor JS Interop 呼叫 |

### 6.7 SortableJS 整合方式

```javascript
// wwwroot/js/kanban-drag.js
window.kanbanDrag = {
    init: function (containerId, dotNetRef) {
        Sortable.create(document.getElementById(containerId), {
            onEnd: function (evt) {
                dotNetRef.invokeMethodAsync('OnEquipmentMoved',
                    evt.item.dataset.equipmentId,
                    evt.newIndex);
            }
        });
    }
};
```

```csharp
// FactoryMapCanvas.razor（編輯模式）
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender && IsEditMode)
        await JS.InvokeVoidAsync("kanbanDrag.init", "map-canvas", DotNetRef);
}

[JSInvokable]
public async Task OnEquipmentMoved(string equipmentId, int newIndex)
{
    await LayoutService.SaveDraftAsync(...);
}
```

---

## 7. 權限控制

使用 `IFunctionPermissionService` 控制以下功能碼：

| 權限碼 | 說明 |
|---|---|
| `KANBAN_VIEW` | 可查看看板地圖 |
| `KANBAN_EDIT` | 可進入編輯模式、拖拉佈局 |
| `KANBAN_PUBLISH` | 可發布草稿版本 |
| `KANBAN_SETTINGS` | 可管理設備、資料來源設定 |

元件內透過注入 `IFunctionPermissionService` 動態顯示或隱藏操作按鈕。

---

## 8. 地圖格式支援

| 格式 | 處理方式 |
|---|---|
| PNG / JPG | `<img>` 標籤顯示，使用者在上方標注設備位置 |
| SVG | Inline `<svg>`，可程式化操作圖層與顏色 |
| DXF | `netDxf` 解析 → 轉換為 SVG 後顯示 |
| JSON 座標定義 | `JsonXmlCoordParser` 解析設備位置，建立 FactoryMap |
| XML 座標定義 | `JsonXmlCoordParser` 解析設備位置，建立 FactoryMap |

---

## 9. 資料來源支援

| 類型 | 適配器 | 備註 |
|---|---|---|
| SQL | `SqlDataAdapter` | `IDbConnectionFactory`（MySQL），強制參數化查詢 |
| CSV | `CsvDataAdapter` | `CsvHelper`，支援自訂分隔符 |
| JSON | `JsonDataAdapter` | `System.Text.Json` + JSONPath 取值 |
| XML | `XmlDataAdapter` | `System.Xml` + XPath 取值 |

---

## 10. 佈局版本管理

- **草稿（Draft）**：編輯模式下儲存，不影響檢視模式
- **已發布（Published）**：同一時間只有一個 Published 版本，發布後舊版本變為 Archived
- **封存（Archived）**：保留歷史版本，可透過 `RollbackAsync` 重新發布
- **版本號**：整數遞增（VersionNo），由 `LayoutService` 自動管理

---

## 11. 範圍外（Out of Scope）

- 使用者帳號管理（由 TPS.Nexus 主系統負責）
- 資料庫 Schema Migration（由呼叫端或 DBA 管理）
- 行動裝置（Mobile）響應式設計
- 多語系（i18n）
- Gauge 儀表盤元件（Radzen 社群版支援有限，視需求評估後續補充）
