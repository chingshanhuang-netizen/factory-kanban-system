const CELL = 20;
const COLS = 20;
const ROWS = 20;

let keyHandler = null;

window.snakeGame = {

    init(dotnetRef) {
        keyHandler = (e) => {
            const handled = ['ArrowUp','ArrowDown','ArrowLeft','ArrowRight',
                             'w','W','a','A','s','S','d','D',' ','Enter'];
            if (handled.includes(e.key)) {
                e.preventDefault();
                dotnetRef.invokeMethodAsync('HandleKey', e.key);
            }
        };
        document.addEventListener('keydown', keyHandler);
    },

    dispose() {
        if (keyHandler) document.removeEventListener('keydown', keyHandler);
        keyHandler = null;
    },

    draw(state) {
        const canvas = document.getElementById('gameCanvas');
        if (!canvas) return;
        const ctx = canvas.getContext('2d');

        ctx.clearRect(0, 0, canvas.width, canvas.height);

        // Grid
        ctx.strokeStyle = '#ffffff08';
        for (let i = 0; i <= COLS; i++) {
            ctx.beginPath(); ctx.moveTo(i * CELL, 0); ctx.lineTo(i * CELL, canvas.height); ctx.stroke();
        }
        for (let j = 0; j <= ROWS; j++) {
            ctx.beginPath(); ctx.moveTo(0, j * CELL); ctx.lineTo(canvas.width, j * CELL); ctx.stroke();
        }

        // Regular food
        const foodColors = ['#ff6b6b', '#ffd93d'];
        state.foods.forEach((f, i) => {
            const c = foodColors[i % foodColors.length];
            ctx.fillStyle = c;
            ctx.shadowColor = c;
            ctx.shadowBlur = 12;
            ctx.beginPath();
            ctx.arc(f.x * CELL + CELL / 2, f.y * CELL + CELL / 2, CELL / 2 - 2, 0, Math.PI * 2);
            ctx.fill();
            ctx.shadowBlur = 0;
        });

        // Super food (star)
        if (state.superFood) {
            const cx = state.superFood.x * CELL + CELL / 2;
            const cy = state.superFood.y * CELL + CELL / 2;
            ctx.fillStyle = '#c77dff';
            ctx.shadowColor = '#c77dff';
            ctx.shadowBlur = 18;
            ctx.beginPath();
            for (let i = 0; i < 10; i++) {
                const angle = (i * Math.PI) / 5 - Math.PI / 2;
                const r = i % 2 === 0 ? CELL / 2 - 1 : CELL / 4;
                i === 0
                    ? ctx.moveTo(cx + Math.cos(angle) * r, cy + Math.sin(angle) * r)
                    : ctx.lineTo(cx + Math.cos(angle) * r, cy + Math.sin(angle) * r);
            }
            ctx.closePath();
            ctx.fill();
            ctx.shadowBlur = 0;
        }

        // Snake body
        state.snake.forEach((seg, i) => {
            const ratio = 1 - i / state.snake.length;
            ctx.fillStyle = i === 0 ? '#4ecca3' : `rgba(78,204,163,${0.3 + ratio * 0.7})`;
            ctx.shadowColor = '#4ecca3';
            ctx.shadowBlur = i === 0 ? 10 : 0;
            const pad = i === 0 ? 1 : 2;
            ctx.beginPath();
            ctx.roundRect(seg.x * CELL + pad, seg.y * CELL + pad, CELL - pad * 2, CELL - pad * 2, 4);
            ctx.fill();
            ctx.shadowBlur = 0;
        });

        // Eyes on head
        if (state.snake.length > 0) {
            const h = state.snake[0];
            const d = state.dir;
            ctx.fillStyle = '#1a1a2e';
            const ex = d.x === 0 ? [5, 13] : d.x > 0 ? [13, 13] : [7, 7];
            const ey = d.y === 0 ? [7, 13] : d.y > 0 ? [13, 13] : [7, 7];
            ctx.beginPath(); ctx.arc(h.x * CELL + ex[0], h.y * CELL + ey[0], 2.5, 0, Math.PI * 2); ctx.fill();
            ctx.beginPath(); ctx.arc(h.x * CELL + ex[1], h.y * CELL + ey[1], 2.5, 0, Math.PI * 2); ctx.fill();
        }

        // Overlays
        if (state.gameState === 'Over') {
            ctx.fillStyle = 'rgba(0,0,0,0.55)';
            ctx.fillRect(0, 0, canvas.width, canvas.height);
            ctx.fillStyle = '#4ecca3';
            ctx.font = 'bold 2.2rem Segoe UI';
            ctx.textAlign = 'center';
            ctx.fillText('GAME OVER', canvas.width / 2, canvas.height / 2 - 20);
            ctx.fillStyle = '#eee';
            ctx.font = '1.1rem Segoe UI';
            ctx.fillText(`得分：${state.score}`, canvas.width / 2, canvas.height / 2 + 20);
            ctx.textAlign = 'left';
        }

        if (state.gameState === 'Paused') {
            ctx.fillStyle = 'rgba(0,0,0,0.45)';
            ctx.fillRect(0, 0, canvas.width, canvas.height);
            ctx.fillStyle = '#4ecca3';
            ctx.font = 'bold 2.2rem Segoe UI';
            ctx.textAlign = 'center';
            ctx.fillText('暫停中', canvas.width / 2, canvas.height / 2);
            ctx.textAlign = 'left';
        }
    }
};
