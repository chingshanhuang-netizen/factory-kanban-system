using TPS.Nexus.Core;

namespace TPS.Nexus.Kanban.Demo.Mocks;

public class DemoPermissionService : IFunctionPermissionService
{
    public Task<bool> HasPermissionAsync(string permissionCode) => Task.FromResult(true);
}
