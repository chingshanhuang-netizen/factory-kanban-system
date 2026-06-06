using TPS.Nexus.Core;

namespace TPS.Nexus.Kanban.Demo.Infrastructure;

public class WebHostEnvironmentAccessor : IWebHostEnvironmentAccessor
{
    public WebHostEnvironmentAccessor(string webRootPath) =>
        WebRootPath = webRootPath;

    public string WebRootPath { get; }
}
