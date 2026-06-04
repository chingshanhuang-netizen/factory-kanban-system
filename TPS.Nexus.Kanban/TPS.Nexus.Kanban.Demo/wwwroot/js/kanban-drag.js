window.kanbanDrag = {
    _dragging:   null,
    _container:  null,
    _dotNetRef:  null,
    _offsetX: 0, _offsetY: 0,
    _startX:  0, _startY:  0,
    _moved:      false,
    _handlers:   {},

    init: function (containerId, dotNetRef) {
        this.destroy();
        var container = document.getElementById(containerId);
        if (!container) return;
        this._container = container;
        this._dotNetRef = dotNetRef;

        var self = this;

        // ── pointerdown: start drag ────────────────────────────────────────────
        this._handlers.down = function (evt) {
            var widget = evt.target.closest('[data-equipment-id]');
            if (!widget) return;
            self._dragging = widget;
            self._moved    = false;
            var r = widget.getBoundingClientRect();
            self._offsetX = evt.clientX - r.left;
            self._offsetY = evt.clientY - r.top;
            self._startX  = evt.clientX;
            self._startY  = evt.clientY;
            widget.style.cursor     = 'grabbing';
            widget.style.zIndex     = '100';
            widget.style.userSelect = 'none';
        };

        // ── pointermove: move widget visually ─────────────────────────────────
        this._handlers.move = function (evt) {
            if (!self._dragging) return;
            if (Math.abs(evt.clientX - self._startX) > 3 ||
                Math.abs(evt.clientY - self._startY) > 3) {
                self._moved = true;
            }
            if (!self._moved) return;
            evt.preventDefault();
            var cr = self._container.getBoundingClientRect();
            var x  = Math.max(0, Math.min(
                evt.clientX - cr.left - self._offsetX,
                cr.width  - self._dragging.offsetWidth));
            var y  = Math.max(0, Math.min(
                evt.clientY - cr.top  - self._offsetY,
                cr.height - self._dragging.offsetHeight));
            self._dragging.style.left = x + 'px';
            self._dragging.style.top  = y + 'px';
        };

        // ── pointerup: persist new position ───────────────────────────────────
        this._handlers.up = function (evt) {
            if (!self._dragging) return;
            var el = self._dragging;
            el.style.cursor     = '';
            el.style.zIndex     = '';
            el.style.userSelect = '';
            if (self._moved) {
                var cr  = self._container.getBoundingClientRect();
                var x   = Math.round(Math.max(0, Math.min(
                    evt.clientX - cr.left - self._offsetX,
                    cr.width  - el.offsetWidth)));
                var y   = Math.round(Math.max(0, Math.min(
                    evt.clientY - cr.top  - self._offsetY,
                    cr.height - el.offsetHeight)));
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

        // down on container; move/up on document so drag works outside bounds
        container.addEventListener('pointerdown', this._handlers.down);
        document.addEventListener('pointermove',  this._handlers.move);
        document.addEventListener('pointerup',    this._handlers.up);
        document.addEventListener('pointercancel',this._handlers.up);
    },

    destroy: function () {
        if (this._container && this._handlers.down)
            this._container.removeEventListener('pointerdown', this._handlers.down);
        if (this._handlers.move) {
            document.removeEventListener('pointermove',   this._handlers.move);
            document.removeEventListener('pointerup',     this._handlers.up);
            document.removeEventListener('pointercancel', this._handlers.up);
        }
        this._dragging  = null;
        this._container = null;
        this._dotNetRef = null;
        this._handlers  = {};
    },

    updatePosition: function (elementId, x, y) {
        var el = document.getElementById(elementId);
        if (el) { el.style.left = x + 'px'; el.style.top = y + 'px'; }
    }
};
