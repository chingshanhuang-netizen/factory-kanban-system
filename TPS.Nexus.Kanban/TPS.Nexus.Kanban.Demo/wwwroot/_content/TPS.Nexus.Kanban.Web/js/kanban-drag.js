window.kanbanDrag = {
    _sortable: null,

    init: function (containerId, dotNetRef) {
        var el = document.getElementById(containerId);
        if (!el) return;
        if (typeof Sortable === 'undefined') {
            console.warn('SortableJS not loaded — drag-drop disabled');
            return;
        }
        this._sortable = Sortable.create(el, {
            animation: 150,
            onEnd: function (evt) {
                var equipmentId = evt.item.dataset.equipmentId;
                var x = evt.originalEvent ? evt.originalEvent.offsetX : 0;
                var y = evt.originalEvent ? evt.originalEvent.offsetY : 0;
                dotNetRef.invokeMethodAsync('OnEquipmentMoved', equipmentId, x, y);
            }
        });
    },

    destroy: function () {
        if (this._sortable) {
            this._sortable.destroy();
            this._sortable = null;
        }
    },

    updatePosition: function (elementId, x, y) {
        var el = document.getElementById(elementId);
        if (el) {
            el.style.left = x + 'px';
            el.style.top  = y + 'px';
        }
    }
};
