using TPS.Nexus.Kanban.Core.Interfaces;

namespace TPS.Nexus.Kanban.Demo.Mocks;

public class DemoIconGalleryService : IIconGalleryService
{
    private readonly List<string> _icons = new();

    public Task AddAsync(string iconUrl)
    {
        if (!string.IsNullOrEmpty(iconUrl) && !_icons.Contains(iconUrl))
            _icons.Add(iconUrl);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<string>> GetAllAsync()
        => Task.FromResult(_icons.AsEnumerable());
}
