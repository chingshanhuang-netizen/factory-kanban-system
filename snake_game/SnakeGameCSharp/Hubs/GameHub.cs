using Microsoft.AspNetCore.SignalR;
using SnakeGameCSharp.Models;
using System.Collections.Concurrent;

namespace SnakeGameCSharp.Hubs;

public class GameHub : Hub
{
    // One game session per browser connection
    private static readonly ConcurrentDictionary<string, (SnakeGame Game, CancellationTokenSource Cts)> _sessions = new();

    // IHubContext<T> is a singleton — safe to capture and use from background tasks
    private readonly IHubContext<GameHub> _hubContext;

    public GameHub(IHubContext<GameHub> hubContext)
    {
        _hubContext = hubContext;
    }

    // ── Connection lifecycle ───────────────────────────────────────────────────

    public override async Task OnConnectedAsync()
    {
        var game = new SnakeGame();
        game.Init();
        _sessions[Context.ConnectionId] = (game, new CancellationTokenSource());
        await PushStateAsync();
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (_sessions.TryRemove(Context.ConnectionId, out var s))
        {
            s.Cts.Cancel();
            s.Cts.Dispose();
        }
        return base.OnDisconnectedAsync(exception);
    }

    // ── Client-invoked methods ─────────────────────────────────────────────────

    public async Task StartGame()
    {
        if (!TryGetSession(out var game, out _)) return;
        if (game.State == GameState.Running) return;

        game.SetRunning();
        ReplaceLoop(out var token);
        await PushStateAsync();

        // Capture singleton before hub method returns — hub instance becomes invalid after return
        _ = GameLoopAsync(Context.ConnectionId, token, _hubContext);
    }

    public Task SteerSnake(int x, int y)
    {
        if (TryGetSession(out var game, out _)) game.Steer(x, y);
        return Task.CompletedTask;
    }

    public async Task TogglePause()
    {
        if (!TryGetSession(out var game, out _)) return;

        if (game.State == GameState.Running)
        {
            if (_sessions.TryGetValue(Context.ConnectionId, out var s)) s.Cts.Cancel();
            game.TogglePause();
            _sessions[Context.ConnectionId] = (game, new CancellationTokenSource());
        }
        else if (game.State == GameState.Paused)
        {
            game.TogglePause();
            ReplaceLoop(out var token);
            _ = GameLoopAsync(Context.ConnectionId, token, _hubContext);
        }

        await PushStateAsync();
    }

    public async Task RestartGame()
    {
        if (!TryGetSession(out var game, out _)) return;

        if (_sessions.TryGetValue(Context.ConnectionId, out var s)) s.Cts.Cancel();
        game.Init();
        _sessions[Context.ConnectionId] = (game, new CancellationTokenSource());
        await PushStateAsync();
    }

    // ── Game loop (static — no hub instance reference) ─────────────────────────

    private static async Task GameLoopAsync(string connectionId, CancellationToken token, IHubContext<GameHub> hubCtx)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(120));
        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                if (!_sessions.TryGetValue(connectionId, out var s)) break;

                var alive = s.Game.Tick();
                await hubCtx.Clients.Client(connectionId).SendAsync("ReceiveState", BuildDto(s.Game), token);

                if (!alive) break;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private bool TryGetSession(out SnakeGame game, out CancellationTokenSource cts)
    {
        if (_sessions.TryGetValue(Context.ConnectionId, out var s))
        {
            game = s.Game; cts = s.Cts; return true;
        }
        game = null!; cts = null!; return false;
    }

    private void ReplaceLoop(out CancellationToken token)
    {
        if (_sessions.TryGetValue(Context.ConnectionId, out var s)) s.Cts.Cancel();
        var newCts = new CancellationTokenSource();
        if (_sessions.TryGetValue(Context.ConnectionId, out var cur))
            _sessions[Context.ConnectionId] = (cur.Game, newCts);
        token = newCts.Token;
    }

    private Task PushStateAsync()
    {
        if (!_sessions.TryGetValue(Context.ConnectionId, out var s)) return Task.CompletedTask;
        return Clients.Caller.SendAsync("ReceiveState", BuildDto(s.Game));
    }

    private static object BuildDto(SnakeGame g) => new
    {
        snake     = g.Snake.Select(p => new { x = p.X, y = p.Y }).ToArray(),
        foods     = g.Foods.Select(p => new { x = p.X, y = p.Y }).ToArray(),
        superFood = g.SuperFood is { } sf ? (object)new { x = sf.X, y = sf.Y } : null,
        dir       = new { x = g.Dir.X, y = g.Dir.Y },
        gameState = g.State.ToString(),
        score     = g.Score,
        best      = g.Best
    };
}
