// Virtual canvas dimensions — all widget positions are stored in this coordinate space.
var KANBAN_VIRT_W = 1920;
var KANBAN_VIRT_H = 1080;

window.kanbanDrag = {
    SNAP_THRESH: 8,

    _dragging:       null,
    _container:      null,
    _virtualCanvas:  null,
    _guideContainer: null,
    _dotNetRef:      null,
    _offsetX: 0, _offsetY: 0,
    _startX:  0, _startY:  0,
    _moved:          false,
    _handlers:       {},

    // Multi-select state
    _selection:      null,   // Set<HTMLElement>
    _dragStartPos:   null,   // Map<HTMLElement, {x,y}>
    _primaryStartX:  0,
    _primaryStartY:  0,
    _contextMenu:    null,

    init: function (containerId, dotNetRef) {
        this.destroy();
        var container = document.getElementById(containerId);
        if (!container) return;
        this._container     = container;
        this._virtualCanvas = container.querySelector('.kanban-virtual-canvas');
        this._dotNetRef     = dotNetRef;
        this._selection     = new Set();

        // Guide overlay — lives inside virtual canvas so it scales with the map
        if (this._virtualCanvas) {
            var gc = document.createElement('div');
            gc.id = 'kanban-snap-guides';
            gc.style.cssText = 'position:absolute;inset:0;pointer-events:none;z-index:150;overflow:visible';
            this._virtualCanvas.appendChild(gc);
            this._guideContainer = gc;
        }

        var self = this;

        // ── pointerdown: start drag or toggle selection ────────────────────────
        this._handlers.down = function (evt) {
            if (evt.button !== 0) return;   // right-click handled by contextmenu event
            var widget = evt.target.closest('[data-equipment-id]');

            if (!widget) {
                if (!evt.ctrlKey && !evt.metaKey) self._clearSelection();
                return;
            }

            // Ctrl / Cmd + click: toggle selection, do not start drag
            if (evt.ctrlKey || evt.metaKey) {
                evt.preventDefault();
                self._toggleSelect(widget);
                // suppress the Blazor @onclick that follows
                document.addEventListener('click', function (e) { e.stopPropagation(); }, { capture: true, once: true });
                return;
            }

            // Regular click: if widget not in selection, clear and select it
            if (!self._selection.has(widget)) {
                self._clearSelection();
                self._addToSelection(widget);
            }

            // Begin drag
            var scaleX = self._container.offsetWidth  / KANBAN_VIRT_W;
            var scaleY = self._container.offsetHeight / KANBAN_VIRT_H;
            self._dragging = widget;
            self._moved    = false;
            var r = widget.getBoundingClientRect();
            self._offsetX = (evt.clientX - r.left) / scaleX;
            self._offsetY = (evt.clientY - r.top)  / scaleY;
            self._startX  = evt.clientX;
            self._startY  = evt.clientY;

            // Snapshot start positions for all selected widgets
            self._dragStartPos  = new Map();
            self._primaryStartX = parseFloat(widget.style.left) || 0;
            self._primaryStartY = parseFloat(widget.style.top)  || 0;
            self._selection.forEach(function (el) {
                self._dragStartPos.set(el, {
                    x: parseFloat(el.style.left) || 0,
                    y: parseFloat(el.style.top)  || 0
                });
                el.style.cursor     = 'grabbing';
                el.style.zIndex     = '100';
                el.style.userSelect = 'none';
            });
        };

        // ── pointermove: move widget(s) + show snap guides ─────────────────────
        this._handlers.move = function (evt) {
            if (!self._dragging) return;
            if (Math.abs(evt.clientX - self._startX) > 3 ||
                Math.abs(evt.clientY - self._startY) > 3) self._moved = true;
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
            x = Math.max(0, Math.min(snap.x, KANBAN_VIRT_W - self._dragging.offsetWidth));
            y = Math.max(0, Math.min(snap.y, KANBAN_VIRT_H - self._dragging.offsetHeight));

            self._dragging.style.left = x + 'px';
            self._dragging.style.top  = y + 'px';

            // Apply same delta to all other selected widgets
            if (self._selection.size > 1 && self._dragStartPos) {
                var dx = x - self._primaryStartX;
                var dy = y - self._primaryStartY;
                self._selection.forEach(function (el) {
                    if (el === self._dragging) return;
                    var s = self._dragStartPos.get(el);
                    if (!s) return;
                    el.style.left = Math.max(0, Math.min(s.x + dx, KANBAN_VIRT_W - el.offsetWidth))  + 'px';
                    el.style.top  = Math.max(0, Math.min(s.y + dy, KANBAN_VIRT_H - el.offsetHeight)) + 'px';
                });
            }

            self._renderGuides(snap.guides);
        };

        // ── pointerup: persist virtual positions, clear guides ─────────────────
        this._handlers.up = function (evt) {
            if (!self._dragging) return;
            var el = self._dragging;

            self._selection.forEach(function (selEl) {
                selEl.style.cursor     = '';
                selEl.style.zIndex     = '';
                selEl.style.userSelect = '';
            });
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

                document.addEventListener('click', function (e) { e.stopPropagation(); }, { capture: true, once: true });

                if (self._selection.size > 1 && self._dragStartPos) {
                    var dx = x - self._primaryStartX;
                    var dy = y - self._primaryStartY;
                    var moves = [];
                    self._selection.forEach(function (selEl) {
                        var s = self._dragStartPos.get(selEl);
                        var fx, fy;
                        if (selEl === el) {
                            fx = x; fy = y;
                        } else {
                            fx = Math.round(Math.max(0, Math.min(s.x + dx, KANBAN_VIRT_W - selEl.offsetWidth)));
                            fy = Math.round(Math.max(0, Math.min(s.y + dy, KANBAN_VIRT_H - selEl.offsetHeight)));
                        }
                        selEl.style.left = fx + 'px';
                        selEl.style.top  = fy + 'px';
                        moves.push({ id: selEl.dataset.equipmentId, x: fx, y: fy });
                    });
                    self._dotNetRef.invokeMethodAsync('OnEquipmentsBatchMoved', moves);
                } else {
                    el.style.left = x + 'px';
                    el.style.top  = y + 'px';
                    self._dotNetRef.invokeMethodAsync('OnEquipmentMoved', el.dataset.equipmentId, x, y);
                }
            }

            self._dragging     = null;
            self._moved        = false;
            self._dragStartPos = null;
        };

        // ── contextmenu: show alignment menu ───────────────────────────────────
        this._handlers.contextmenu = function (evt) {
            evt.preventDefault();
            var widget = evt.target.closest('[data-equipment-id]');
            if (widget && !self._selection.has(widget)) {
                self._clearSelection();
                self._addToSelection(widget);
            }
            if (self._selection.size === 0) { self._hideContextMenu(); return; }
            self._showContextMenu(evt.clientX, evt.clientY);
        };

        // ── dismiss context menu on outside click ───────────────────────────────
        this._handlers.dismiss = function (evt) {
            if (self._contextMenu && !self._contextMenu.contains(evt.target))
                self._hideContextMenu();
        };

        // ── Escape: clear selection + hide menu ─────────────────────────────────
        this._handlers.keydown = function (evt) {
            if (evt.key === 'Escape') {
                self._hideContextMenu();
                self._clearSelection();
            }
        };

        container.addEventListener('pointerdown',  this._handlers.down);
        container.addEventListener('contextmenu',  this._handlers.contextmenu);
        document.addEventListener('pointermove',   this._handlers.move);
        document.addEventListener('pointerup',     this._handlers.up);
        document.addEventListener('pointercancel', this._handlers.up);
        document.addEventListener('pointerdown',   this._handlers.dismiss);
        document.addEventListener('keydown',       this._handlers.keydown);
    },

    // ── Selection helpers ──────────────────────────────────────────────────────
    _addToSelection: function (el) {
        this._selection.add(el);
        el.classList.add('kanban-widget--selected');
    },
    _removeFromSelection: function (el) {
        this._selection.delete(el);
        el.classList.remove('kanban-widget--selected');
    },
    _toggleSelect: function (el) {
        if (this._selection.has(el)) this._removeFromSelection(el);
        else this._addToSelection(el);
    },
    _clearSelection: function () {
        this._selection.forEach(function (el) { el.classList.remove('kanban-widget--selected'); });
        this._selection.clear();
    },

    // ── Context menu ──────────────────────────────────────────────────────────
    _showContextMenu: function (cx, cy) {
        this._hideContextMenu();
        var self  = this;
        var count = this._selection.size;

        var menu = document.createElement('div');
        menu.id = 'kanban-ctx-menu';
        menu.style.cssText =
            'position:fixed;z-index:9999;left:' + cx + 'px;top:' + cy + 'px;' +
            'background:#0d1b2a;border:1px solid #1a3a5c;border-radius:8px;' +
            'padding:4px 0 6px;min-width:176px;' +
            'box-shadow:0 4px 24px rgba(0,0,0,.75);' +
            'font-family:"Segoe UI",Arial,sans-serif;font-size:13px;' +
            'color:#c8d8e8;user-select:none;';

        // Header
        var hdr = document.createElement('div');
        hdr.style.cssText = 'padding:7px 14px 6px;color:#4a7fa8;font-size:11px;border-bottom:1px solid #1a3a5c;margin-bottom:3px;';
        hdr.textContent = '已選取 ' + count + ' 個設備';
        menu.appendChild(hdr);

        var items = [
            { icon: '▲', label: '向上對齊',     fn: '_alignTop',     min: 2 },
            { icon: '▼', label: '向下對齊',     fn: '_alignBottom',  min: 2 },
            { icon: '◀', label: '靠左對齊',     fn: '_alignLeft',    min: 2 },
            { icon: '▶', label: '靠右對齊',     fn: '_alignRight',   min: 2 },
            { sep: true },
            { icon: '↔', label: '水平置中',     fn: '_centerH',      min: 2 },
            { icon: '↕', label: '垂直置中',     fn: '_centerV',      min: 2 },
            { sep: true },
            { icon: '⟺', label: '水平等距分佈', fn: '_distributeH',  min: 3 },
            { icon: '⇕', label: '垂直等距分佈', fn: '_distributeV',  min: 3 },
        ];

        items.forEach(function (item) {
            if (item.sep) {
                var s = document.createElement('div');
                s.style.cssText = 'border-top:1px solid #1a3a5c;margin:3px 0;';
                menu.appendChild(s);
                return;
            }
            var disabled = count < item.min;
            var row = document.createElement('div');
            row.style.cssText =
                'padding:7px 14px;display:flex;align-items:center;gap:10px;' +
                'cursor:' + (disabled ? 'default' : 'pointer') + ';' +
                'color:' + (disabled ? '#2d4a6a' : '#c8d8e8') + ';';
            row.innerHTML =
                '<span style="width:16px;text-align:center;opacity:' + (disabled ? '.3' : '1') + '">' +
                item.icon + '</span><span>' + item.label + '</span>';
            if (!disabled) {
                row.addEventListener('mouseenter', function () { this.style.background = '#132a42'; });
                row.addEventListener('mouseleave', function () { this.style.background = ''; });
                row.addEventListener('mousedown', function (e) {
                    e.preventDefault(); e.stopPropagation();
                    self._hideContextMenu();
                    self[item.fn]();
                });
            }
            menu.appendChild(row);
        });

        document.body.appendChild(menu);
        this._contextMenu = menu;

        // Adjust position to keep within viewport
        requestAnimationFrame(function () {
            if (!self._contextMenu) return;
            var rect = menu.getBoundingClientRect();
            var vw = window.innerWidth, vh = window.innerHeight;
            if (rect.right  > vw - 4) menu.style.left = Math.max(0, cx - rect.width)  + 'px';
            if (rect.bottom > vh - 4) menu.style.top  = Math.max(0, cy - rect.height) + 'px';
        });
    },

    _hideContextMenu: function () {
        if (this._contextMenu) {
            if (this._contextMenu.parentNode) this._contextMenu.parentNode.removeChild(this._contextMenu);
            this._contextMenu = null;
        }
    },

    // ── Alignment / distribution actions ──────────────────────────────────────
    _alignTop: function () {
        var els = Array.from(this._selection);
        var min = Math.min.apply(null, els.map(function (e) { return parseFloat(e.style.top) || 0; }));
        var moves = els.map(function (e) {
            e.style.top = min + 'px';
            return { id: e.dataset.equipmentId, x: Math.round(parseFloat(e.style.left) || 0), y: Math.round(min) };
        });
        this._dotNetRef.invokeMethodAsync('OnEquipmentsBatchMoved', moves);
    },

    _alignBottom: function () {
        var els = Array.from(this._selection);
        var max = Math.max.apply(null, els.map(function (e) {
            return (parseFloat(e.style.top) || 0) + e.offsetHeight;
        }));
        var moves = els.map(function (e) {
            var t = max - e.offsetHeight;
            e.style.top = t + 'px';
            return { id: e.dataset.equipmentId, x: Math.round(parseFloat(e.style.left) || 0), y: Math.round(t) };
        });
        this._dotNetRef.invokeMethodAsync('OnEquipmentsBatchMoved', moves);
    },

    _alignLeft: function () {
        var els = Array.from(this._selection);
        var min = Math.min.apply(null, els.map(function (e) { return parseFloat(e.style.left) || 0; }));
        var moves = els.map(function (e) {
            e.style.left = min + 'px';
            return { id: e.dataset.equipmentId, x: Math.round(min), y: Math.round(parseFloat(e.style.top) || 0) };
        });
        this._dotNetRef.invokeMethodAsync('OnEquipmentsBatchMoved', moves);
    },

    _alignRight: function () {
        var els = Array.from(this._selection);
        var max = Math.max.apply(null, els.map(function (e) {
            return (parseFloat(e.style.left) || 0) + e.offsetWidth;
        }));
        var moves = els.map(function (e) {
            var l = max - e.offsetWidth;
            e.style.left = l + 'px';
            return { id: e.dataset.equipmentId, x: Math.round(l), y: Math.round(parseFloat(e.style.top) || 0) };
        });
        this._dotNetRef.invokeMethodAsync('OnEquipmentsBatchMoved', moves);
    },

    _centerH: function () {
        var els = Array.from(this._selection);
        var avg = els.reduce(function (s, e) {
            return s + (parseFloat(e.style.left) || 0) + e.offsetWidth / 2;
        }, 0) / els.length;
        var moves = els.map(function (e) {
            var l = avg - e.offsetWidth / 2;
            e.style.left = l + 'px';
            return { id: e.dataset.equipmentId, x: Math.round(l), y: Math.round(parseFloat(e.style.top) || 0) };
        });
        this._dotNetRef.invokeMethodAsync('OnEquipmentsBatchMoved', moves);
    },

    _centerV: function () {
        var els = Array.from(this._selection);
        var avg = els.reduce(function (s, e) {
            return s + (parseFloat(e.style.top) || 0) + e.offsetHeight / 2;
        }, 0) / els.length;
        var moves = els.map(function (e) {
            var t = avg - e.offsetHeight / 2;
            e.style.top = t + 'px';
            return { id: e.dataset.equipmentId, x: Math.round(parseFloat(e.style.left) || 0), y: Math.round(t) };
        });
        this._dotNetRef.invokeMethodAsync('OnEquipmentsBatchMoved', moves);
    },

    _distributeH: function () {
        var els = Array.from(this._selection).sort(function (a, b) {
            return (parseFloat(a.style.left) || 0) - (parseFloat(b.style.left) || 0);
        });
        if (els.length < 3) return;
        var start  = parseFloat(els[0].style.left) || 0;
        var end    = (parseFloat(els[els.length - 1].style.left) || 0) + els[els.length - 1].offsetWidth;
        var totalW = els.reduce(function (s, e) { return s + e.offsetWidth; }, 0);
        var gap    = (end - start - totalW) / (els.length - 1);
        var moves  = [], cursor = start;
        els.forEach(function (e) {
            e.style.left = cursor + 'px';
            moves.push({ id: e.dataset.equipmentId, x: Math.round(cursor), y: Math.round(parseFloat(e.style.top) || 0) });
            cursor += e.offsetWidth + gap;
        });
        this._dotNetRef.invokeMethodAsync('OnEquipmentsBatchMoved', moves);
    },

    _distributeV: function () {
        var els = Array.from(this._selection).sort(function (a, b) {
            return (parseFloat(a.style.top) || 0) - (parseFloat(b.style.top) || 0);
        });
        if (els.length < 3) return;
        var start  = parseFloat(els[0].style.top) || 0;
        var end    = (parseFloat(els[els.length - 1].style.top) || 0) + els[els.length - 1].offsetHeight;
        var totalH = els.reduce(function (s, e) { return s + e.offsetHeight; }, 0);
        var gap    = (end - start - totalH) / (els.length - 1);
        var moves  = [], cursor = start;
        els.forEach(function (e) {
            e.style.top = cursor + 'px';
            moves.push({ id: e.dataset.equipmentId, x: Math.round(parseFloat(e.style.left) || 0), y: Math.round(cursor) });
            cursor += e.offsetHeight + gap;
        });
        this._dotNetRef.invokeMethodAsync('OnEquipmentsBatchMoved', moves);
    },

    // ── Snap + guide calculation ───────────────────────────────────────────────
    // Phase 1: edge/center alignment (cyan) — 9×9 pairs per axis
    // Phase 2: equal spacing (amber)        — only for non-aligned axes
    // Selected widgets (moving together) are excluded from snap targets.
    _calcSnap: function (x, y, dragEl) {
        if (!this._virtualCanvas) return { x: x, y: y, guides: [] };

        var dw  = dragEl.offsetWidth;
        var dh  = dragEl.offsetHeight;
        var dL  = x,          dCX = x + dw / 2, dR  = x + dw;
        var dT  = y,          dCY = y + dh / 2, dB  = y + dh;

        var bestX = { delta: this.SNAP_THRESH, guide: null };
        var bestY = { delta: this.SNAP_THRESH, guide: null };
        var others = [];

        var widgets = this._virtualCanvas.querySelectorAll('[data-equipment-id]');
        for (var i = 0; i < widgets.length; i++) {
            var el = widgets[i];
            if (el === dragEl) continue;
            if (this._selection && this._selection.has(el)) continue;  // co-selected — moving together

            var ox  = parseFloat(el.style.left) || 0;
            var oy  = parseFloat(el.style.top)  || 0;
            var ow  = el.offsetWidth;
            var oh  = el.offsetHeight;
            var oL  = ox, oCX = ox + ow / 2, oR  = ox + ow;
            var oT  = oy, oCY = oy + oh / 2, oB  = oy + oh;

            others.push({ L: ox, R: oR, T: oy, B: oB });

            var xPairs = [
                [dL,oL],[dL,oCX],[dL,oR],
                [dCX,oL],[dCX,oCX],[dCX,oR],
                [dR,oL],[dR,oCX],[dR,oR]
            ];
            for (var j = 0; j < xPairs.length; j++) {
                var dx = xPairs[j][1] - xPairs[j][0];
                if (Math.abs(dx) < Math.abs(bestX.delta)) bestX = { delta: dx, guide: xPairs[j][1] };
            }

            var yPairs = [
                [dT,oT],[dT,oCY],[dT,oB],
                [dCY,oT],[dCY,oCY],[dCY,oB],
                [dB,oT],[dB,oCY],[dB,oB]
            ];
            for (var k = 0; k < yPairs.length; k++) {
                var dy = yPairs[k][1] - yPairs[k][0];
                if (Math.abs(dy) < Math.abs(bestY.delta)) bestY = { delta: dy, guide: yPairs[k][1] };
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

        if (bestX.guide === null) {
            var eqX = this._calcEqualSpacingX(snappedX, dw, others);
            if (eqX !== null) {
                snappedX = eqX.snap;
                var midY = snappedY + dh / 2;
                guides.push({ type: 'hgap', x1: eqX.A.R, x2: snappedX,      y: midY });
                guides.push({ type: 'hgap', x1: snappedX + dw, x2: eqX.B.L, y: midY });
            }
        }
        if (bestY.guide === null) {
            var eqY = this._calcEqualSpacingY(snappedY, dh, others);
            if (eqY !== null) {
                snappedY = eqY.snap;
                var midX = snappedX + dw / 2;
                guides.push({ type: 'vgap', y1: eqY.A.B, y2: snappedY,      x: midX });
                guides.push({ type: 'vgap', y1: snappedY + dh, y2: eqY.B.T, x: midX });
            }
        }

        return { x: snappedX, y: snappedY, guides: guides };
    },

    _calcEqualSpacingX: function (x, dw, others) {
        var dL = x, dR = x + dw;
        var bestDelta = this.SNAP_THRESH;
        var found = false, bestSnap, bestA, bestB;
        for (var a = 0; a < others.length; a++) {
            for (var b = 0; b < others.length; b++) {
                if (a === b) continue;
                var A = others[a], B = others[b];
                if (A.R > dL || B.L < dR) continue;
                var delta = (dL - A.R) - (B.L - dR);
                if (Math.abs(delta) < Math.abs(bestDelta)) {
                    bestDelta = delta;
                    bestSnap = A.R + (B.L - A.R - dw) / 2;
                    bestA = A; bestB = B; found = true;
                }
            }
        }
        return found ? { snap: bestSnap, A: bestA, B: bestB } : null;
    },

    _calcEqualSpacingY: function (y, dh, others) {
        var dT = y, dB = y + dh;
        var bestDelta = this.SNAP_THRESH;
        var found = false, bestSnap, bestA, bestB;
        for (var a = 0; a < others.length; a++) {
            for (var b = 0; b < others.length; b++) {
                if (a === b) continue;
                var A = others[a], B = others[b];
                if (A.B > dT || B.T < dB) continue;
                var delta = (dT - A.B) - (B.T - dB);
                if (Math.abs(delta) < Math.abs(bestDelta)) {
                    bestDelta = delta;
                    bestSnap = A.B + (B.T - A.B - dh) / 2;
                    bestA = A; bestB = B; found = true;
                }
            }
        }
        return found ? { snap: bestSnap, A: bestA, B: bestB } : null;
    },

    // ── Render guide lines ─────────────────────────────────────────────────────
    // Cyan  → edge/center alignment  (type: 'v' | 'h')
    // Amber → equal spacing          (type: 'hgap' | 'vgap')
    _renderGuides: function (guides) {
        var gc = this._guideContainer;
        if (!gc) return;
        gc.innerHTML = '';
        for (var i = 0; i < guides.length; i++) {
            var g = guides[i];
            if (g.type === 'h') {
                var line = document.createElement('div');
                line.style.cssText =
                    'position:absolute;left:-40px;right:-40px;top:' + g.pos + 'px;height:0;' +
                    'border-top:1px dashed rgba(0,229,255,.9);box-shadow:0 0 4px rgba(0,229,255,.45);';
                gc.appendChild(line);
            } else if (g.type === 'v') {
                var line = document.createElement('div');
                line.style.cssText =
                    'position:absolute;top:-40px;bottom:-40px;left:' + g.pos + 'px;width:0;' +
                    'border-left:1px dashed rgba(0,229,255,.9);box-shadow:0 0 4px rgba(0,229,255,.45);';
                gc.appendChild(line);
            } else if (g.type === 'hgap') {
                var w = g.x2 - g.x1;
                if (w < 2) continue;
                var line = document.createElement('div');
                line.style.cssText =
                    'position:absolute;left:' + g.x1 + 'px;top:' + g.y + 'px;' +
                    'width:' + w + 'px;height:0;' +
                    'border-top:1px dashed rgba(255,183,0,.9);box-shadow:0 0 3px rgba(255,183,0,.4);';
                gc.appendChild(line);
                var t1 = document.createElement('div');
                t1.style.cssText = 'position:absolute;left:' + g.x1 + 'px;top:' + (g.y - 5) + 'px;width:0;height:10px;border-left:1.5px solid rgba(255,183,0,.9);';
                gc.appendChild(t1);
                var t2 = document.createElement('div');
                t2.style.cssText = 'position:absolute;left:' + g.x2 + 'px;top:' + (g.y - 5) + 'px;width:0;height:10px;border-left:1.5px solid rgba(255,183,0,.9);';
                gc.appendChild(t2);
            } else if (g.type === 'vgap') {
                var h = g.y2 - g.y1;
                if (h < 2) continue;
                var line = document.createElement('div');
                line.style.cssText =
                    'position:absolute;left:' + g.x + 'px;top:' + g.y1 + 'px;' +
                    'width:0;height:' + h + 'px;' +
                    'border-left:1px dashed rgba(255,183,0,.9);box-shadow:0 0 3px rgba(255,183,0,.4);';
                gc.appendChild(line);
                var t3 = document.createElement('div');
                t3.style.cssText = 'position:absolute;left:' + (g.x - 5) + 'px;top:' + g.y1 + 'px;width:10px;height:0;border-top:1.5px solid rgba(255,183,0,.9);';
                gc.appendChild(t3);
                var t4 = document.createElement('div');
                t4.style.cssText = 'position:absolute;left:' + (g.x - 5) + 'px;top:' + g.y2 + 'px;width:10px;height:0;border-top:1.5px solid rgba(255,183,0,.9);';
                gc.appendChild(t4);
            }
        }
    },

    destroy: function () {
        this._hideContextMenu();
        if (this._selection) { this._clearSelection(); this._selection = null; }
        if (this._guideContainer) {
            if (this._guideContainer.parentNode) this._guideContainer.parentNode.removeChild(this._guideContainer);
            this._guideContainer = null;
        }
        if (this._container && this._handlers.down) {
            this._container.removeEventListener('pointerdown', this._handlers.down);
            this._container.removeEventListener('contextmenu', this._handlers.contextmenu);
        }
        if (this._handlers.move) {
            document.removeEventListener('pointermove',   this._handlers.move);
            document.removeEventListener('pointerup',     this._handlers.up);
            document.removeEventListener('pointercancel', this._handlers.up);
            document.removeEventListener('pointerdown',   this._handlers.dismiss);
            document.removeEventListener('keydown',       this._handlers.keydown);
        }
        this._dragging      = null;
        this._container     = null;
        this._virtualCanvas = null;
        this._dotNetRef     = null;
        this._handlers      = {};
        this._dragStartPos  = null;
        this._contextMenu   = null;
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
