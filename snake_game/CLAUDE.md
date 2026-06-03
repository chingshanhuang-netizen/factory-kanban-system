# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build

# Run (starts on http://localhost:8000)
dotnet run

# Build release
dotnet build -c Release
dotnet run -c Release

# Stop server — find and kill the process on port 8000
$procId = (netstat -ano | Select-String ":8000\s.*LISTENING" | ForEach-Object { ($_ -split '\s+')[-1] } | Select-Object -First 1)
Stop-Process -Id $procId -Force
```

All commands should be run from `SnakeGameCSharp/`.

## Architecture

**Stack:** ASP.NET Core MVC + SignalR + .NET 10. Game logic runs entirely on the server; the browser only renders Canvas.

### Data flow

```
Browser keypresses
  → game-client.js (SignalR invoke)
  → GameHub (Hub method)
  → SnakeGame.Tick() / Steer()
  → BuildDto() serialised to JSON
  → SignalR push → ReceiveState event
  → game-client.js updates DOM scores + calls snakeGame.draw(state)
  → snake.js renders Canvas
```

### Key design decisions

**Hub is transient — game loop is not.**  
`GameHub` instances are created per-method-call and disposed on return. `GameLoopAsync` is `static` and receives `IHubContext<GameHub>` (singleton) as a parameter so it can keep pushing state after `StartGame()` returns. Never use `Clients` or `Context` inside `GameLoopAsync`.

**Per-connection sessions.**  
`_sessions` (`ConcurrentDictionary<connectionId, (SnakeGame, CancellationTokenSource)>`) holds one game per browser tab. `OnDisconnectedAsync` cancels and disposes the CTS. `RestartGame` calls `game.Init()` on the existing instance (not `new SnakeGame()`) so `Best` score survives restarts within the same session.

**Growth via `PendingGrowth`.**  
Instead of special-casing food eaten vs. not, every tick pops the tail unless `PendingGrowth > 0`. Eating regular food adds 1; eating super food adds 2.

### Files that matter

| File | Responsibility |
|------|---------------|
| `Models/SnakeGame.cs` | Pure game logic — `Tick()`, `Steer()`, `TogglePause()`, food spawning. No I/O. |
| `Models/Point.cs` | `record Point(int X, int Y)` — value-equality used for collision checks via `List.Contains`. |
| `Hubs/GameHub.cs` | SignalR hub: session management, game loop lifecycle, `BuildDto` serialisation. |
| `Controllers/HomeController.cs` | Single `Index()` action — serves `Views/Home/Index.cshtml`. |
| `wwwroot/snake.js` | Canvas drawing. Accepts a state DTO: `{ snake, foods, superFood, dir, gameState, score, best }`. |
| `wwwroot/game-client.js` | SignalR connection, keyboard/button wiring, local `currentState` tracking for correct hub method dispatch. |

### SignalR contract

Client → Server (hub method names):

- `StartGame()` — transitions `Waiting → Running`, starts `GameLoopAsync`
- `SteerSnake(x, y)` — updates `NextDir`; direction change takes effect on next `Tick()`
- `TogglePause()` — `Running ↔ Paused`; cancels/restarts the loop
- `RestartGame()` — calls `Init()`, cancels loop, pushes fresh state

Server → Client (event name `ReceiveState`): fires every 120 ms during gameplay, and once after every hub method that mutates state.
