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

    public Task DeleteAsync(string iconUrl)
    {
        _icons.Remove(iconUrl);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(string oldUrl, string newUrl)
    {
        var idx = _icons.IndexOf(oldUrl);
        if (idx >= 0) _icons[idx] = newUrl;
        else if (!string.IsNullOrEmpty(newUrl)) _icons.Add(newUrl);
        return Task.CompletedTask;
    }
}
