using Microsoft.AspNetCore.SignalR;

namespace TPS.Nexus.Kanban.Services.Hubs;

public class KanbanAlarmHub : Hub
{
    public async Task SendAlarmAsync(int equipmentId, string alarmLevel, string message)
    {
        await Clients.All.SendAsync("ReceiveAlarm", equipmentId, alarmLevel, message);
    }
}
