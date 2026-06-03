// Stub drag implementation for demo mode.
// Production host provides a real drag implementation backed by mouse/pointer events.
window.kanbanDrag = {
    init: function (containerId, dotnetRef) {
        console.log('[KanbanDrag] init on container:', containerId);
    },
    destroy: function () {
        console.log('[KanbanDrag] destroy');
    }
};
