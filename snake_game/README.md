# 貪食蛇遊戲 操作手冊

## 專案簡介

以 **ASP.NET Core MVC + SignalR + .NET 10** 開發的瀏覽器貪食蛇遊戲。  
遊戲邏輯完全在 C# 伺服器端執行，透過 SignalR 即時推送畫面至瀏覽器，前端只負責渲染 Canvas。

---

## 系統需求

| 項目 | 需求 |
|------|------|
| 作業系統 | Windows 10 以上 |
| .NET SDK | 10.0 以上 |
| 瀏覽器 | Chrome / Edge / Firefox（支援 WebSocket） |

確認 .NET 版本：

```bash
dotnet --version
```

---

## 啟動伺服器

```bash
cd SnakeGameCSharp
dotnet run
```

啟動後開啟瀏覽器前往：

```
http://localhost:8000
```

停止伺服器請按 `Ctrl + C`。

---

## 遊戲操作

### 鍵盤

| 按鍵 | 動作 |
|------|------|
| `↑` `↓` `←` `→` | 控制蛇的移動方向 |
| `W` `A` `S` `D` | 同方向鍵 |
| `空白鍵` | 開始遊戲 / 暫停 / 繼續 |
| `Enter` | 開始遊戲 / 重新開始（遊戲結束後） |

### 手機 / 觸控

畫面上的 ▲ ◀ ▼ ▶ 按鈕可控制方向，「重新開始」按鈕可重置遊戲。

---

## 遊戲規則

### 基本規則

- 蛇每隔 120ms 自動前進一格
- 吃到食物後蛇身增長，並獲得分數
- 蛇頭撞到**自己的身體**時遊戲結束
- 蛇碰到牆壁會**穿透**至對面繼續移動

### 食物系統

| 食物 | 外觀 | 得分 | 說明 |
|------|------|------|------|
| 普通食物 | 🔴 紅色圓形 | +1 | 場上始終保持 2 個 |
| 普通食物 | 🟡 黃色圓形 | +1 | 吃掉後立即補出新的 |
| 超級食物 | ⭐ 紫色星形 | +2 | 每吃掉 2 個普通食物後出現，蛇身增長 2 節 |

---

## 專案架構

```
SnakeGameCSharp/
├── Controllers/
│   └── HomeController.cs      # 提供遊戲頁面（MVC Controller）
├── Hubs/
│   └── GameHub.cs             # SignalR Hub，管理遊戲迴圈與即時通訊
├── Models/
│   ├── Point.cs               # 座標資料結構
│   └── SnakeGame.cs           # 遊戲邏輯（蛇、食物、移動、碰撞）
├── Views/
│   └── Home/
│       └── Index.cshtml       # 遊戲頁面 HTML
├── wwwroot/
│   ├── snake.js               # Canvas 渲染（繪圖）
│   └── game-client.js         # SignalR 客戶端，處理鍵盤輸入與畫面更新
├── Properties/
│   └── launchSettings.json    # 啟動設定（Port 8000）
└── Program.cs                 # 應用程式進入點，註冊 MVC + SignalR
```

### MVC 分層說明

```
瀏覽器（View）
    │  鍵盤輸入 → SignalR → Hub
    │  ← 遊戲狀態 ← SignalR ←
    ▼
game-client.js          # 接收狀態、繪製 Canvas、傳送操作指令
snake.js                # Canvas 繪圖邏輯

伺服器（Controller + Model）
    │
    ├── HomeController.cs      # 回傳 Index 頁面
    ├── GameHub.cs             # 接收指令、驅動遊戲迴圈、推送狀態
    └── SnakeGame.cs           # 純遊戲邏輯（Tick、Steer、Spawn）
```

---

## 技術說明

### 遊戲迴圈

每個瀏覽器連線在伺服器端擁有獨立的 `SnakeGame` 實例與 `PeriodicTimer`，每 120ms 執行一次 `Tick()`，計算結果透過 SignalR 推送至對應的瀏覽器。

### SignalR Hub 生命週期

Hub 為 transient（短暫）物件，每次方法呼叫後即失效。背景遊戲迴圈改用注入的 `IHubContext<GameHub>`（singleton）推送訊息，確保連線不中斷。

### Hub 方法對應表

| 瀏覽器呼叫 | Hub 方法 | 說明 |
|-----------|----------|------|
| `StartGame()` | `StartGame()` | 啟動遊戲迴圈 |
| `SteerSnake(x, y)` | `SteerSnake()` | 改變移動方向 |
| `TogglePause()` | `TogglePause()` | 暫停 / 繼續 |
| `RestartGame()` | `RestartGame()` | 重置遊戲（保留最高分） |

---

## 常見問題

**Q：頁面顯示「連線失敗」？**  
A：確認伺服器已啟動（`dotnet run`），並檢查瀏覽器是否支援 WebSocket。

**Q：最高分重新整理後消失？**  
A：最高分儲存於伺服器的連線 Session，重新整理（新連線）後會重置。

**Q：如何更改 Port？**  
A：修改 `Properties/launchSettings.json` 中的 `applicationUrl`，或在 `Program.cs` 的 `app.Run()` 指定新的位址。
