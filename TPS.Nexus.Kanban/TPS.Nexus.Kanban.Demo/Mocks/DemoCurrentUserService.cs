using TPS.Nexus.Kanban.Core.Interfaces;

namespace TPS.Nexus.Kanban.Demo.Mocks;

public class DemoCurrentUserService : ICurrentUserService
{
    public string GetCurrentUser() => "Demo 管理員";
}
