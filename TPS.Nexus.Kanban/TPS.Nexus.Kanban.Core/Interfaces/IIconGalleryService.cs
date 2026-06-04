namespace TPS.Nexus.Kanban.Core.Interfaces;

public interface IIconGalleryService
{
    Task AddAsync(string iconUrl);
    Task<IEnumerable<string>> GetAllAsync();
}
