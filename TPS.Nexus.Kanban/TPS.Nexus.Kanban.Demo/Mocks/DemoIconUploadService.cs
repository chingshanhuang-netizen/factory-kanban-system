using TPS.Nexus.Kanban.Core.Interfaces;

namespace TPS.Nexus.Kanban.Demo.Mocks;

public class DemoIconUploadService : IIconUploadService
{
    private static readonly Dictionary<string, string> _mimeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".png",  "image/png"       },
        { ".jpg",  "image/jpeg"      },
        { ".jpeg", "image/jpeg"      },
        { ".svg",  "image/svg+xml"   },
        { ".gif",  "image/gif"       },
        { ".webp", "image/webp"      },
    };

    public async Task<string> UploadAsync(Stream file, string fileName)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("fileName 不能為空。", nameof(fileName));

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var bytes = ms.ToArray();

        var ext  = Path.GetExtension(fileName);
        var mime = _mimeMap.TryGetValue(ext, out var m) ? m : "image/png";

        return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
    }

    public Task DeleteAsync(string filePath) => Task.CompletedTask;
}
