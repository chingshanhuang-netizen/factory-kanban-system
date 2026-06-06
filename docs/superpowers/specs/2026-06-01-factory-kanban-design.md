# TPS.Nexus 工廠看板系統模組 — 設計規格

**日期**: 2026-06-01（更新：2026-06-07）
**版本**: 1.3
**狀態**: 已核准

---

## 異動記錄

| 版本 | 日期 | 異動內容 |
|---|---|---|
| 1.0 | 2026-06-01 | 初版 |
| 1.1 | 2026-06-01 | Equipment 位置欄位移至 EquipmentWidget |
| 1.2 | 2026-06-02 | 宿主確認為 ASP.NET Core MVC；UI 庫由 DevExpress 改為 Radzen Blazor + SortableJS；資料庫確認為 MySQL；模組目錄結構更新 |
| 1.3 | 2026-06-07 | 新增地圖輪播功能（FactoryMap + KanbanMapPage）；FactoryMapCanvas 加入輪播徽章（全螢幕修正）；工具列改用 native button/select（Radzen CSS 覆蓋問題）；LayoutVersionPanel 改為深色抽屜風格；地圖管理新增版本欄位；MainLayout NavLink 修正；新增 UserPrefsService；DataGrid header 高度對齊 |

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
Version           // string, e.g. "v1"、"v2"，顯示於地圖管理版本欄
CarouselEnabled   // bool，是否加入輪播序列
CarouselSeconds   // int，在此地圖停留秒數（預設 10）
CarouselOrder     // int，輪播排序（升冪）
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

### 4.4 UserPrefsService（Web 層，非 Core 介面）

```csharp
// TPS.Nexus.Kanban.Web/Services/UserPrefsService.cs
// 透過 IJSRuntime 讀寫 localStorage，跨地圖導覽保持使用者最後選取的地圖 ID
public class UserPrefsService
{
    public int? SelectedMapId { get; private set; }
    Task EnsureLoadedAsync(IJSRuntime js)   // 首次加載時從 localStorage 讀取
    void SetSelectedMapId(int id)
    Task SaveAsync(IJSRuntime js)            // 寫入 localStorage
}
```

---

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
<div id="kanban-fs-wrapper" style="position:relative;overflow:hidden">  ← 全螢幕容器（overflow:hidden 避免 drawer 溢出）
  ① <MapLayer />                                        ← 底層：廠區樓層圖
     PNG/JPG → <img>；SVG → inline <svg>；DXF → 轉換後 <svg>
  ② @foreach widget → <EquipmentWidgetPanel />          ← 設備層（position:absolute）
     style="left:@widget.PositionX px; top:@widget.PositionY px"
  ③ <EquipmentTooltip />                                ← 互動層（最上層）
     <EquipmentDetailDrawer />
  ④ 輪播倒數徽章（position:absolute，right:16px bottom:16px）← 在 wrapper 內部，全螢幕不消失
</div>
```

**全螢幕修正說明**：CSS `position:fixed` 元素在全螢幕 API 下若在 fullscreen element 之外則不可見。
輪播徽章改為 `position:absolute` 並放在 `#kanban-fs-wrapper` 內部，確保全螢幕模式正常顯示。

**FactoryMapCanvas 輪播相關 Parameters**：
```csharp
[Parameter] public bool CarouselVisible { get; set; }
[Parameter] public bool CarouselPaused { get; set; }
[Parameter] public int CarouselRemainingSeconds { get; set; }
[Parameter] public EventCallback OnCarouselTogglePause { get; set; }
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
| `KanbanMapPage.razor` | `/kanban/{mapId}` | 主看板頁：地圖畫布 + 工具列 + 版本疊加面板 + 輪播管理 |
| `KanbanSettingsPage.razor` | `/kanban/settings` | 設定：設備管理、資料來源、圖示上傳、地圖管理（含版本欄位） |

**KanbanMapPage 重要設計決策**：
- **版本面板**：`position:absolute` 疊加於地圖之上（`z-index:300`），寬 320px，開啟時暫停輪播，關閉時恢復
- **版本面板疊加容器**：`position:relative; height:calc(100vh - 102px)` 的父 div，FactoryMapCanvas 與版本面板為兄弟元素
- **輪播**：`PeriodicTimer` 每秒 tick，到 0 時呼叫 `NavigationManager.NavigateTo` 切換到下一張地圖
- **暫停場景**：進入編輯模式、開啟版本面板、使用者手動暫停
- **地圖切換狀態重置**：`OnParametersSetAsync` 重置 `IsEditMode`、`ShowVersions`、`_carouselPaused`
- **UserPrefsService**：記錄最後使用的地圖 ID，頁面初始化時若與 URL 不同則 redirect

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
  MapEditorToolbar.razor    — 工具列：編輯/檢視切換、草稿/發布、版本按鈕
                              ⚠ 使用 native <button> + inline style（非 RadzenButton）
                              ⚠ 多地圖時使用 native <select>（非 RadzenDropDown）
                              原因：Radzen CSS 在相同優先級下覆蓋 custom CSS，inline style 是唯一可靠解法
  DraggableEquipmentItem.razor — SortableJS 驅動的可拖拉設備節點
  WidgetConfigurator.razor  — RadzenAccordion：設定元件組合與資料源

Version/
  LayoutVersionPanel.razor  — 深色抽屜風格版本清單（非 RadzenDataGrid）
                              與 EquipmentDetailDrawer 風格一致：深色背景、flex row、狀態 badge
                              寬度 320px，疊加地圖（position:absolute）不縮放地圖區域

Alarm/
  AlarmBadge.razor          — 設備角標（有警報時顯示）
  AlarmPanel.razor          — 側欄警報清單
  AlarmToast.razor          — RadzenNotification：新警報即時通知
```

### 6.6 Radzen 元件對應

| 功能 | 元件 | 備註 |
|---|---|---|
| 趨勢圖表（Drawer） | `RadzenChart` | 折線/柱狀/面積 |
| Hover 快覽圖（Tooltip） | `RadzenChart`（小尺寸） | 嵌入 Tooltip 內 |
| Hover 快覽面板 | `RadzenTooltip` | |
| 詳情側欄 | `RadzenSidebar` | |
| 警報通知 | `RadzenNotification` | |
| 資料清單（工單/警報） | `RadzenDataGrid` | |
| 版本清單 | **native HTML**（自訂 flex 列表） | ⚠ 不用 RadzenDataGrid，深色抽屜風格 |
| 檔案上傳 | `RadzenUpload` | |
| 工具列按鈕 | **native `<button>`** + inline style | ⚠ 不用 RadzenButton，避免 Radzen CSS 覆蓋 |
| 地圖下拉選單 | **native `<select>`** + inline style | ⚠ 不用 RadzenDropDown，避免輪播後顏色異常 |
| 頁籤 | `RadzenTabs` | |
| 右鍵選單 | `RadzenContextMenu` | |
| 元件設定面板 | `RadzenAccordion` | |
| 設定表單 | `RadzenTemplateForm` + `RadzenTextBox` | |
| **拖拉放置** | **SortableJS**（JS 套件） | Blazor JS Interop 呼叫 |

**Radzen CSS 覆蓋問題說明**：
Radzen 樣式表在自訂 CSS 之後載入，對同等優先級的 `!important` 規則 Radzen 勝出。
解決方案：工具列按鈕、地圖選單改用 native HTML 元素搭配 inline style（inline style 優先級最高）。

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

## 10. 地圖輪播功能

**資料模型擴充**（`FactoryMap`）：
- `CarouselEnabled` (`bool`) — 此地圖是否加入輪播
- `CarouselSeconds` (`int`) — 停留秒數，預設 10
- `CarouselOrder` (`int`) — 排序，升冪，`0` 優先

**KanbanMapPage 輪播邏輯**：
```
StartCarousel()
  → 篩選 AllMaps.Where(m => m.CarouselEnabled)，依 CarouselOrder 排序
  → 建立 CancellationTokenSource + PeriodicTimer（1 秒 tick）
  → 每 tick: _carouselRemainingSeconds--，StateHasChanged
  → 歸零時: Navigation.NavigateTo("/kanban/{nextMapId}")
  → NavigateTo 觸發 OnParametersSetAsync → 重置狀態 → StartCarousel 重啟
```

**暫停規則**：
| 場景 | CarouselPaused | 計時器 |
|---|---|---|
| 進入編輯模式 | false | 停止（不顯示徽章） |
| 開啟版本面板 | true | 停止（顯示暫停徽章） |
| 使用者點暫停 | true | 停止（顯示暫停徽章） |
| 切換地圖（OnParametersSetAsync） | false | 重新啟動 |

**輪播徽章位置**：放在 `#kanban-fs-wrapper` 內（`position:absolute; bottom:16px; right:16px`），全螢幕模式可見。

---

## 11. 佈局版本管理

- **草稿（Draft）**：編輯模式下儲存，不影響檢視模式
- **已發布（Published）**：同一時間只有一個 Published 版本，發布後舊版本變為 Archived
- **封存（Archived）**：保留歷史版本，可透過 `RollbackAsync` 重新發布
- **版本號**：整數遞增（VersionNo），由 `LayoutService` 自動管理

---

## 12. MainLayout NavLink 設計

`TPS.Nexus.Kanban.Demo/MainLayout.razor` 的「看板地圖」連結**不能用 `NavLink`**：

- `NavLink` 以 `href="/kanban/1"` 為基準，只在 `/kanban/1` 時套用 `active` class
- 輪播切換到 `/kanban/2` 後，active class 消失 → 顯示藍色（未點擊色）

**正確做法**：
```csharp
// 普通 <a> + NavigationManager.LocationChanged 訂閱
private bool IsKanbanActive
{
    get {
        var path = new Uri(Nav.Uri).LocalPath;
        return path.StartsWith("/kanban/") && !path.StartsWith("/kanban/settings");
    }
}
```
- 任何 `/kanban/{n}` URL 皆套用 active（白色）
- LocationChanged 事件觸發 `InvokeAsync(StateHasChanged)` 保持反應性
- 實作 `IDisposable` 於 Dispose 中取消事件訂閱

---

## 13. DataGrid 高度規格

`kanban-light-grid` 主題：header row 與 data row 均為 **38px**，由 CSS 強制設定：
```css
.kanban-light-grid .rz-grid-table thead th { height: 38px !important; }
.kanban-light-grid .rz-grid-table td       { height: 38px !important; }
```
字體大小 12px，`line-height: 38px` 垂直置中。

---

## 14. 範圍外（Out of Scope）

- 使用者帳號管理（由 TPS.Nexus 主系統負責）
- 資料庫 Schema Migration（由呼叫端或 DBA 管理）
- 行動裝置（Mobile）響應式設計
- 多語系（i18n）
- Gauge 儀表盤元件（Radzen 社群版支援有限，視需求評估後續補充）
