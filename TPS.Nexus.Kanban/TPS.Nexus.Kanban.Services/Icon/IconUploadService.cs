using TPS.Nexus.Kanban.Core.Interfaces;
namespace TPS.Nexus.Kanban.Services.Icon;
public class IconUploadService : IIconUploadService
{
    public Task<string> UploadAsync(System.IO.Stream file, string fileName) => throw new NotImplementedException();
    public Task DeleteAsync(string filePath) => throw new NotImplementedException();
}
