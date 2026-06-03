# Task 0: Host Prerequisites — 工程師操作指南

## 需要修改的現有檔案

### 1. `TPS.Nexus.Core/Interfaces/IModuleRegistrar.cs`

在現有介面新增 `MapEndpoints` 方法：

```csharp
public interface IModuleRegistrar
{
    void RegisterServices(IServiceCollection services, IConfiguration configuration);
    void MapEndpoints(IEndpointRouteBuilder endpoints); // ← 新增此行
}
```

**所有現有模組的 `*ModuleRegistrar.cs` 都需加入空實作：**

```csharp
public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
```

---

### 2. `TPS.Nexus.Web/Program.cs`

在 `builder.Services.AddControllersWithViews()` 之後加入：

```csharp
builder.Services.AddServerSideBlazor();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSignalR();
```

在 `app.MapControllerRoute(...)` 之前加入：

```csharp
app.MapBlazorHub();
foreach (var module in modules)
{
    module.Registrar.MapEndpoints(app);
}
```

---

## 確認清單

- [ ] `IModuleRegistrar` 新增 `MapEndpoints` 方法
- [ ] 所有既有模組加入空的 `MapEndpoints` 實作
- [ ] `Program.cs` 加入 Blazor Server 服務
- [ ] `Program.cs` 加入 SignalR 服務
- [ ] `Program.cs` 加入 `MapBlazorHub()` 呼叫
- [ ] `Program.cs` 加入模組 `MapEndpoints` 迴圈
- [ ] 宿主專案加入 `Radzen.Blazor` 的靜態資源引用（`_Host.cshtml` 或 `App.razor`）：
  ```html
  <link rel="stylesheet" href="_content/Radzen.Blazor/css/default.css">
  <script src="_content/Radzen.Blazor/Radzen.Blazor.js"></script>
  ```
