// SignalR client — bridges the browser UI to the C# GameHub (MVC + SignalR)

let currentState = 'Waiting';

const connection = new signalR.HubConnectionBuilder()
    .withUrl('/gamehub')
    .withAutomaticReconnect()
    .build();

// ── Receive state from server ──────────────────────────────────────────────────

connection.on('ReceiveState', (state) => {
    currentState = state.gameState;

    snakeGame.draw(state);

    document.getElementById('score').textContent = state.score;
    document.getElementById('best').textContent  = state.best;
    document.getElementById('msg').innerHTML     = getMessage(state);
});

function getMessage(state) {
    switch (state.gameState) {
        case 'Waiting': return '按 <b>空白鍵</b> 或方向鍵開始遊戲';
        case 'Running': return '';
        case 'Paused':  return '⏸ 已暫停&nbsp;&nbsp;按 <b>空白鍵</b> 繼續';
        case 'Over':    return `遊戲結束！得分：<b>${state.score}</b>&nbsp;&nbsp;按 <b>Enter</b> 重新開始`;
        default:        return '';
    }
}

// ── Keyboard input ─────────────────────────────────────────────────────────────

document.addEventListener('keydown', async (e) => {
    const dir = {
        ArrowUp: [0,-1], w: [0,-1], W: [0,-1],
        ArrowDown: [0,1], s: [0,1], S: [0,1],
        ArrowLeft: [-1,0], a: [-1,0], A: [-1,0],
        ArrowRight: [1,0], d: [1,0], D: [1,0],
    }[e.key];

    if (dir) {
        e.preventDefault();
        if (currentState === 'Waiting') {
            await invoke('SteerSnake', ...dir);
            await invoke('StartGame');
        } else if (currentState === 'Running' || currentState === 'Paused') {
            await invoke('SteerSnake', ...dir);
        }
        return;
    }

    if (e.key === ' ') {
        e.preventDefault();
        if      (currentState === 'Waiting')               await invoke('StartGame');
        else if (currentState === 'Running' || currentState === 'Paused') await invoke('TogglePause');
    }

    if (e.key === 'Enter') {
        e.preventDefault();
        if      (currentState === 'Waiting') await invoke('StartGame');
        else if (currentState === 'Over')    await invoke('RestartGame');
    }
});

// ── Mobile buttons ─────────────────────────────────────────────────────────────

async function mobileSteer(x, y) {
    if (currentState === 'Waiting') {
        await invoke('SteerSnake', x, y);
        await invoke('StartGame');
    } else if (currentState === 'Running' || currentState === 'Paused') {
        await invoke('SteerSnake', x, y);
    } else if (currentState === 'Over') {
        await invoke('RestartGame');
    }
}

document.getElementById('btn-up').addEventListener('click',    () => mobileSteer(0, -1));
document.getElementById('btn-left').addEventListener('click',  () => mobileSteer(-1, 0));
document.getElementById('btn-down').addEventListener('click',  () => mobileSteer(0,  1));
document.getElementById('btn-right').addEventListener('click', () => mobileSteer(1,  0));
document.getElementById('btn-restart').addEventListener('click', () => invoke('RestartGame'));

// ── Helpers ────────────────────────────────────────────────────────────────────

async function invoke(method, ...args) {
    try { await connection.invoke(method, ...args); }
    catch (e) { console.error(method, e); }
}

// ── Start connection ───────────────────────────────────────────────────────────

(async () => {
    try {
        await connection.start();
        console.log('SignalR 已連線');
    } catch (e) {
        console.error('SignalR 連線失敗', e);
        document.getElementById('msg').textContent = '連線失敗，請重新整理頁面';
    }
})();
