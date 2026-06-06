// Virtual canvas dimensions — all widget positions are stored in this coordinate space.
var KANBAN_VIRT_W = 1920;
var KANBAN_VIRT_H = 1080;

window.kanbanDrag = {
    SNAP_THRESH: 8,   // virtual pixels — distance within which a snap activates

    _dragging:      null,
    _container:     null,
    _virtualCanvas: null,
    _guideContainer:null,
    _dotNetRef:     null,
    _offsetX: 0, _offsetY: 0,
    _startX:  0, _startY:  0,
    _moved:         false,
    _handlers:      {},

    init: function (containerId, dotNetRef) {
        this.destroy();
        var container = document.getElementById(containerId);
        if (!container) return;
        this._container     = container;
        this._virtualCanvas = container.querySelector('.kanban-virtual-canvas');
        this._dotNetRef     = dotNetRef;

        // Guide overlay — lives inside the virtual canvas so it scales with the map
        if (this._virtualCanvas) {
            var gc = document.createElement('div');
            gc.id = 'kanban-snap-guides';
            gc.style.cssText = 'position:absolute;inset:0;pointer-events:none;z-index:150;overflow:visible';
            this._virtualCanvas.appendChild(gc);
            this._guideContainer = gc;
        }

        var self = this;

        // ── pointerdown: start drag ────────────────────────────────────────────
        this._handlers.down = function (evt) {
            var widget = evt.target.closest('[data-equipment-id]');
            if (!widget) return;
            var scaleX = self._container.offsetWidth  / KANBAN_VIRT_W;
            var scaleY = self._container.offsetHeight / KANBAN_VIRT_H;
            self._dragging = widget;
            self._moved    = false;
            var r = widget.getBoundingClientRect();
            // Offset stored in virtual canvas space
            self._offsetX = (evt.clientX - r.left) / scaleX;
            self._offsetY = (evt.clientY - r.top)  / scaleY;
            self._startX  = evt.clientX;
            self._startY  = evt.clientY;
            widget.style.cursor     = 'grabbing';
            widget.style.zIndex     = '100';
            widget.style.userSelect = 'none';
        };

        // ── pointermove: move widget + show snap guides ────────────────────────
        this._handlers.move = function (evt) {
            if (!self._dragging) return;
            if (Math.abs(evt.clientX - self._startX) > 3 ||
                Math.abs(evt.clientY - self._startY) > 3) {
                self._moved = true;
            }
            if (!self._moved) return;
            evt.preventDefault();

            var scaleX = self._container.offsetWidth  / KANBAN_VIRT_W;
            var scaleY = self._container.offsetHeight / KANBAN_VIRT_H;
            var cr     = self._container.getBoundingClientRect();
            var x = Math.max(0, Math.min(
                (evt.clientX - cr.left) / scaleX - self._offsetX,
                KANBAN_VIRT_W - self._dragging.offsetWidth));
            var y = Math.max(0, Math.min(
                (evt.clientY - cr.top)  / scaleY - self._offsetY,
                KANBAN_VIRT_H - self._dragging.offsetHeight));

            var snap = self._calcSnap(x, y, self._dragging);
            // Re-clamp after snap offset to stay within canvas
            x = Math.max(0, Math.min(snap.x, KANBAN_VIRT_W - self._dragging.offsetWidth));
            y = Math.max(0, Math.min(snap.y, KANBAN_VIRT_H - self._dragging.offsetHeight));
            self._renderGuides(snap.guides);

            self._dragging.style.left = x + 'px';
            self._dragging.style.top  = y + 'px';
        };

        // ── pointerup: persist virtual position, clear guides ─────────────────
        this._handlers.up = function (evt) {
            if (!self._dragging) return;
            var el = self._dragging;
            el.style.cursor     = '';
            el.style.zIndex     = '';
            el.style.userSelect = '';
            self._renderGuides([]);

            if (self._moved) {
                var scaleX = self._container.offsetWidth  / KANBAN_VIRT_W;
                var scaleY = self._container.offsetHeight / KANBAN_VIRT_H;
                var cr     = self._container.getBoundingClientRect();
                var x = Math.max(0, Math.min(
                    (evt.clientX - cr.left) / scaleX - self._offsetX,
                    KANBAN_VIRT_W - el.offsetWidth));
                var y = Math.max(0, Math.min(
                    (evt.clientY - cr.top)  / scaleY - self._offsetY,
                    KANBAN_VIRT_H - el.offsetHeight));

                var snap = self._calcSnap(x, y, el);
                x = Math.round(Math.max(0, Math.min(snap.x, KANBAN_VIRT_W - el.offsetWidth)));
                y = Math.round(Math.max(0, Math.min(snap.y, KANBAN_VIRT_H - el.offsetHeight)));

                var eid = el.dataset.equipmentId;
                // suppress the Blazor @onclick that fires right after pointerup
                document.addEventListener('click',
                    function (e) { e.stopPropagation(); },
                    { capture: true, once: true });
                self._dotNetRef.invokeMethodAsync('OnEquipmentMoved', eid, x, y);
            }
            self._dragging = null;
            self._moved    = false;
        };

        container.addEventListener('pointerdown', this._handlers.down);
        document.addEventListener('pointermove',  this._handlers.move);
        document.addEventListener('pointerup',    this._handlers.up);
        document.addEventListener('pointercancel',this._handlers.up);
    },

    // ── Snap + guide calculation ───────────────────────────────────────────────
    // Phase 1: edge/center alignment (cyan guides, 9×9 pairs per axis)
    // Phase 2: equal spacing (amber guides, only for axes that didn't align-snap)
    _calcSnap: function (x, y, dragEl) {
        if (!this._virtualCanvas) return { x: x, y: y, guides: [] };

        var dw  = dragEl.offsetWidth;
        var dh  = dragEl.offsetHeight;
        var dL  = x,          dCX = x + dw / 2, dR  = x + dw;
        var dT  = y,          dCY = y + dh / 2, dB  = y + dh;

        var bestX = { delta: this.SNAP_THRESH, guide: null };
        var bestY = { delta: this.SNAP_THRESH, guide: null };

        // Collect other widgets for both alignment and spacing checks
        var others = [];
        var widgets = this._virtualCanvas.querySelectorAll('[data-equipment-id]');

        for (var i = 0; i < widgets.length; i++) {
            var el = widgets[i];
            if (el === dragEl) continue;

            var ox  = parseFloat(el.style.left) || 0;
            var oy  = parseFloat(el.style.top)  || 0;
            var ow  = el.offsetWidth;
            var oh  = el.offsetHeight;
            var oL  = ox, oCX = ox + ow / 2, oR  = ox + ow;
            var oT  = oy, oCY = oy + oh / 2, oB  = oy + oh;

            others.push({ L: ox, R: oR, T: oy, B: oB, CX: oCX, CY: oCY, W: ow, H: oh });

            // ── Phase 1: alignment snap (9 pairs per axis) ─────────────────
            var xPairs = [
                [dL,oL],[dL,oCX],[dL,oR],
                [dCX,oL],[dCX,oCX],[dCX,oR],
                [dR,oL],[dR,oCX],[dR,oR]
            ];
            for (var j = 0; j < xPairs.length; j++) {
                var dx = xPairs[j][1] - xPairs[j][0];
                if (Math.abs(dx) < Math.abs(bestX.delta)) {
                    bestX = { delta: dx, guide: xPairs[j][1] };
                }
            }

            var yPairs = [
                [dT,oT],[dT,oCY],[dT,oB],
                [dCY,oT],[dCY,oCY],[dCY,oB],
                [dB,oT],[dB,oCY],[dB,oB]
            ];
            for (var k = 0; k < yPairs.length; k++) {
                var dy = yPairs[k][1] - yPairs[k][0];
                if (Math.abs(dy) < Math.abs(bestY.delta)) {
                    bestY = { delta: dy, guide: yPairs[k][1] };
                }
            }
        }

        var guides   = [];
        var snappedX = x;
        var snappedY = y;

        if (bestX.guide !== null) {
            snappedX = x + bestX.delta;
            guides.push({ type: 'v', pos: bestX.guide });
        }
        if (bestY.guide !== null) {
            snappedY = y + bestY.delta;
            guides.push({ type: 'h', pos: bestY.guide });
        }

        // ── Phase 2: equal-spacing snap (only for non-aligned axes) ───────────
        if (bestX.guide === null) {
            var eqX = this._calcEqualSpacingX(snappedX, dw, others);
            if (eqX !== null) {
                snappedX = eqX.snap;
                var midY = snappedY + dh / 2;
                guides.push({ type: 'hgap', x1: eqX.A.R, x2: snappedX,       y: midY });
                guides.push({ type: 'hgap', x1: snappedX + dw, x2: eqX.B.L,  y: midY });
            }
        }
        if (bestY.guide === null) {
            var eqY = this._calcEqualSpacingY(snappedY, dh, others);
            if (eqY !== null) {
                snappedY = eqY.snap;
                var midX = snappedX + dw / 2;
                guides.push({ type: 'vgap', y1: eqY.A.B, y2: snappedY,       x: midX });
                guides.push({ type: 'vgap', y1: snappedY + dh, y2: eqY.B.T,  x: midX });
            }
        }

        return { x: snappedX, y: snappedY, guides: guides };
    },

    // Find a pair (A left, B right) whose gaps to the dragged widget are equal within threshold.
    // Returns { snap, A, B } or null.
    _calcEqualSpacingX: function (x, dw, others) {
        var dL = x, dR = x + dw;
        var bestDelta = this.SNAP_THRESH;
        var found = false, bestSnap, bestA, bestB;

        for (var a = 0; a < others.length; a++) {
            for (var b = 0; b < others.length; b++) {
                if (a === b) continue;
                var A = others[a], B = others[b];
                // A must be fully left of drag, B fully right of drag
                if (A.R > dL || B.L < dR) continue;
                var gapL  = dL - A.R;          // gap between A's right edge and drag's left edge
                var gapR  = B.L - dR;          // gap between drag's right edge and B's left edge
                var delta = gapL - gapR;
                if (Math.abs(delta) < Math.abs(bestDelta)) {
                    bestDelta = delta;
                    bestSnap  = A.R + (B.L - A.R - dw) / 2;
                    bestA = A; bestB = B;
                    found = true;
                }
            }
        }

        return found ? { snap: bestSnap, A: bestA, B: bestB } : null;
    },

    // Find a pair (A above, B below) whose gaps to the dragged widget are equal within threshold.
    // Returns { snap, A, B } or null.
    _calcEqualSpacingY: function (y, dh, others) {
        var dT = y, dB = y + dh;
        var bestDelta = this.SNAP_THRESH;
        var found = false, bestSnap, bestA, bestB;

        for (var a = 0; a < others.length; a++) {
            for (var b = 0; b < others.length; b++) {
                if (a === b) continue;
                var A = others[a], B = others[b];
                // A must be fully above drag, B fully below drag
                if (A.B > dT || B.T < dB) continue;
                var gapT  = dT - A.B;
                var gapB  = B.T - dB;
                var delta = gapT - gapB;
                if (Math.abs(delta) < Math.abs(bestDelta)) {
                    bestDelta = delta;
                    bestSnap  = A.B + (B.T - A.B - dh) / 2;
                    bestA = A; bestB = B;
                    found = true;
                }
            }
        }

        return found ? { snap: bestSnap, A: bestA, B: bestB } : null;
    },

    // ── Render / clear guide lines inside the virtual canvas ──────────────────
    // Cyan dashed lines  → edge/center alignment (type: 'v' | 'h')
    // Amber dashed lines → equal spacing        (type: 'hgap' | 'vgap')
    _renderGuides: function (guides) {
        var gc = this._guideContainer;
        if (!gc) return;
        gc.innerHTML = '';

        for (var i = 0; i < guides.length; i++) {
            var g = guides[i];

            if (g.type === 'h') {
                // Horizontal alignment line — full width at Y = g.pos
                var line = document.createElement('div');
                line.style.cssText =
                    'position:absolute;left:-40px;right:-40px;' +
                    'top:' + g.pos + 'px;height:0;' +
                    'border-top:1px dashed rgba(0,229,255,.9);' +
                    'box-shadow:0 0 4px rgba(0,229,255,.45);';
                gc.appendChild(line);

            } else if (g.type === 'v') {
                // Vertical alignment line — full height at X = g.pos
                var line = document.createElement('div');
                line.style.cssText =
                    'position:absolute;top:-40px;bottom:-40px;' +
                    'left:' + g.pos + 'px;width:0;' +
                    'border-left:1px dashed rgba(0,229,255,.9);' +
                    'box-shadow:0 0 4px rgba(0,229,255,.45);';
                gc.appendChild(line);

            } else if (g.type === 'hgap') {
                // Horizontal equal-spacing gap: amber dashed line + tick marks at both ends
                var w = g.x2 - g.x1;
                if (w < 2) continue;

                var line = document.createElement('div');
                line.style.cssText =
                    'position:absolute;' +
                    'left:' + g.x1 + 'px;top:' + g.y + 'px;' +
                    'width:' + w + 'px;height:0;' +
                    'border-top:1px dashed rgba(255,183,0,.9);' +
                    'box-shadow:0 0 3px rgba(255,183,0,.4);';
                gc.appendChild(line);

                var t1 = document.createElement('div');
                t1.style.cssText =
                    'position:absolute;' +
                    'left:' + g.x1 + 'px;top:' + (g.y - 5) + 'px;' +
                    'width:0;height:10px;' +
                    'border-left:1.5px solid rgba(255,183,0,.9);';
                gc.appendChild(t1);

                var t2 = document.createElement('div');
                t2.style.cssText =
                    'position:absolute;' +
                    'left:' + g.x2 + 'px;top:' + (g.y - 5) + 'px;' +
                    'width:0;height:10px;' +
                    'border-left:1.5px solid rgba(255,183,0,.9);';
                gc.appendChild(t2);

            } else if (g.type === 'vgap') {
                // Vertical equal-spacing gap: amber dashed line + tick marks at both ends
                var h = g.y2 - g.y1;
                if (h < 2) continue;

                var line = document.createElement('div');
                line.style.cssText =
                    'position:absolute;' +
                    'left:' + g.x + 'px;top:' + g.y1 + 'px;' +
                    'width:0;height:' + h + 'px;' +
                    'border-left:1px dashed rgba(255,183,0,.9);' +
                    'box-shadow:0 0 3px rgba(255,183,0,.4);';
                gc.appendChild(line);

                var t3 = document.createElement('div');
                t3.style.cssText =
                    'position:absolute;' +
                    'left:' + (g.x - 5) + 'px;top:' + g.y1 + 'px;' +
                    'width:10px;height:0;' +
                    'border-top:1.5px solid rgba(255,183,0,.9);';
                gc.appendChild(t3);

                var t4 = document.createElement('div');
                t4.style.cssText =
                    'position:absolute;' +
                    'left:' + (g.x - 5) + 'px;top:' + g.y2 + 'px;' +
                    'width:10px;height:0;' +
                    'border-top:1.5px solid rgba(255,183,0,.9);';
                gc.appendChild(t4);
            }
        }
    },

    destroy: function () {
        if (this._guideContainer) {
            if (this._guideContainer.parentNode)
                this._guideContainer.parentNode.removeChild(this._guideContainer);
            this._guideContainer = null;
        }
        if (this._container && this._handlers.down)
            this._container.removeEventListener('pointerdown', this._handlers.down);
        if (this._handlers.move) {
            document.removeEventListener('pointermove',   this._handlers.move);
            document.removeEventListener('pointerup',     this._handlers.up);
            document.removeEventListener('pointercancel', this._handlers.up);
        }
        this._dragging      = null;
        this._container     = null;
        this._virtualCanvas = null;
        this._dotNetRef     = null;
        this._handlers      = {};
    },

    updatePosition: function (elementId, x, y) {
        var el = document.getElementById(elementId);
        if (el) { el.style.left = x + 'px'; el.style.top = y + 'px'; }
    }
};

// ── Canvas utilities: resize observer + fullscreen ────────────────────────────
window.kanbanCanvas = {
    _resizeObserver: null,
    _fsHandler:      null,

    observeResize: function (containerId, dotNetRef) {
        this.unobserveResize();
        var container = document.getElementById(containerId);
        if (!container) return;
        var obs = new ResizeObserver(function (entries) {
            for (var entry of entries) {
                var w = entry.contentRect.width;
                var h = entry.contentRect.height;
                if (w > 0 && h > 0)
                    dotNetRef.invokeMethodAsync('OnCanvasResized', w, h);
            }
        });
        obs.observe(container);
        this._resizeObserver = obs;
        // Fire immediately with current size so initial scale is applied
        var r = container.getBoundingClientRect();
        if (r.width > 0 && r.height > 0)
            dotNetRef.invokeMethodAsync('OnCanvasResized', r.width, r.height);
    },

    unobserveResize: function () {
        if (this._resizeObserver) {
            this._resizeObserver.disconnect();
            this._resizeObserver = null;
        }
    },

    observeFullscreen: function (dotNetRef) {
        if (this._fsHandler)
            document.removeEventListener('fullscreenchange', this._fsHandler);
        this._fsHandler = function () {
            dotNetRef.invokeMethodAsync('OnFullscreenChanged', !!document.fullscreenElement);
        };
        document.addEventListener('fullscreenchange', this._fsHandler);
    },

    toggleFullscreen: function (wrapperId) {
        var el = document.getElementById(wrapperId);
        if (!el) return;
        if (!document.fullscreenElement) {
            el.requestFullscreen().catch(function (err) {
                console.warn('kanbanCanvas: fullscreen request failed:', err);
            });
        } else {
            document.exitFullscreen();
        }
    }
};
