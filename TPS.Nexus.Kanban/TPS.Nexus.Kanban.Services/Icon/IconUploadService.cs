using TPS.Nexus.Kanban.Core.Interfaces;

namespace TPS.Nexus.Kanban.Services.Icon;

public class IconUploadService : IIconUploadService
{
    private readonly string _iconDir;

    public IconUploadService(IWebHostEnvironmentAccessor envAccessor)
    {
        _iconDir = Path.Combine(envAccessor.WebRootPath, "images", "equipment-icons");
        Directory.CreateDirectory(_iconDir);
    }

    public async Task<string> UploadAsync(Stream file, string fileName)
    {
        // IU-2: null stream produces NullReferenceException at file.CopyToAsync
        ArgumentNullException.ThrowIfNull(file);

        // IU-1: null/empty fileName produces ArgumentNullException in Path.GetExtension
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("fileName must not be null or whitespace.", nameof(fileName));

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext is not ".png" and not ".jpg" and not ".jpeg" and not ".svg")
            throw new InvalidOperationException("Only PNG, JPG, and SVG icons are supported.");

        var savedName = $"{Guid.NewGuid()}{ext}";
        var fullPath  = Path.Combine(_iconDir, savedName);

        await using var fs = File.Create(fullPath);
        await file.CopyToAsync(fs);

        return $"/module-assets/TPS.Nexus.Kanban/images/equipment-icons/{savedName}";
    }

    public Task DeleteAsync(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var fullPath = Path.Combine(_iconDir, fileName);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }
}
