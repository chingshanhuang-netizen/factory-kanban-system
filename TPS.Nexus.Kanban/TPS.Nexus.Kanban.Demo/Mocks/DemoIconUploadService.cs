using TPS.Nexus.Kanban.Core.Interfaces;

namespace TPS.Nexus.Kanban.Demo.Mocks;

public class DemoIconUploadService : IIconUploadService
{
    public Task<string> UploadAsync(Stream file, string fileName)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("fileName 不能為空。", nameof(fileName));

        // Return a simulated URL — no file is actually written in Demo mode
        return Task.FromResult($"/icons/demo-{Guid.NewGuid():N}{Path.GetExtension(fileName)}");
    }

    public Task DeleteAsync(string filePath) => Task.CompletedTask;
}
