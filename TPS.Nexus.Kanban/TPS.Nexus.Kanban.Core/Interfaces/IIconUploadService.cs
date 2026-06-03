namespace TPS.Nexus.Kanban.Core.Interfaces;

public interface IIconUploadService
{
    Task<string> UploadAsync(Stream file, string fileName);
    Task DeleteAsync(string filePath);
}
