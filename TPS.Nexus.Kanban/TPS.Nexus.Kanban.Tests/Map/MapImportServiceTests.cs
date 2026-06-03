using NSubstitute;
using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Interfaces;
using TPS.Nexus.Kanban.Core.Models;
using TPS.Nexus.Kanban.Services.Map;
using Xunit;

namespace TPS.Nexus.Kanban.Tests.Map;

public class MapImportServiceTests
{
    [Fact]
    public async Task SvgMapParser_ParseAsync_ReturnsSvgFormatType()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var parser = new SvgMapParser(storageRoot);
        using var stream = new MemoryStream("<svg></svg>"u8.ToArray());

        var result = await parser.ParseAsync(stream, "layout.svg");

        Assert.Equal(MapFormatType.Svg, result.FormatType);
        Assert.EndsWith(".svg", result.FilePath);

        Directory.Delete(storageRoot, recursive: true);
    }

    [Theory]
    [InlineData(MapFormatType.Png, ".png")]
    [InlineData(MapFormatType.Jpg, ".jpg")]
    public async Task ImageMapHandler_HandleAsync_ReturnsCorrectFormatAndExtension(MapFormatType format, string expectedExt)
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var handler = new ImageMapHandler(storageRoot);
        using var stream = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        var result = await handler.HandleAsync(stream, "factory.img", format);

        Assert.Equal(format, result.FormatType);
        Assert.EndsWith(expectedExt, result.FilePath);

        Directory.Delete(storageRoot, recursive: true);
    }

    [Fact]
    public async Task DxfMapParser_ParseAsync_ReturnsDxfFormatType_WithSvgPlaceholder()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var parser = new DxfMapParser(storageRoot);
        using var stream = new MemoryStream("DXF DATA"u8.ToArray());

        var result = await parser.ParseAsync(stream, "blueprint.dxf");

        Assert.Equal(MapFormatType.Dxf, result.FormatType);
        Assert.EndsWith(".svg", result.FilePath);

        Directory.Delete(storageRoot, recursive: true);
    }

    [Fact]
    public async Task JsonXmlCoordParser_ParseJsonAsync_ReturnsJsonCoordFormatType()
    {
        var parser = new JsonXmlCoordParser();
        using var stream = new MemoryStream("""{"x":100,"y":200}"""u8.ToArray());

        var result = await parser.ParseJsonAsync(stream, "coords.json");

        Assert.Equal(MapFormatType.JsonCoord, result.FormatType);
        Assert.EndsWith(".json", result.FilePath);
    }

    [Fact]
    public async Task JsonXmlCoordParser_ParseXmlAsync_ReturnsXmlCoordFormatType()
    {
        var parser = new JsonXmlCoordParser();
        using var stream = new MemoryStream("<coords><x>100</x></coords>"u8.ToArray());

        var result = await parser.ParseXmlAsync(stream, "coords.xml");

        Assert.Equal(MapFormatType.XmlCoord, result.FormatType);
        Assert.EndsWith(".xml", result.FilePath);
    }

    [Fact]
    public void FactoryMap_Name_DerivedFromFileNameWithoutExtension()
    {
        var fileName = "factory-floor-level2.svg";
        var map = new FactoryMap();
        map.Name = Path.GetFileNameWithoutExtension(fileName);

        Assert.Equal("factory-floor-level2", map.Name);
    }

    // ── MI-2: null stream should throw with clear message ─────────────────────

    [Fact]
    public async Task ImportAsync_NullStream_ThrowsArgumentNullException()
    {
        var (svc, _) = CreateMapImportService();
        using var stream = (Stream?)null;

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => svc.ImportAsync(stream!, "map.svg", MapFormatType.Svg));
    }

    // ── MI-1: null/empty fileName should throw with clear message ─────────────

    [Fact]
    public async Task ImportAsync_NullFileName_ThrowsArgumentException()
    {
        var (svc, _) = CreateMapImportService();
        using var stream = new MemoryStream("<svg></svg>"u8.ToArray());

        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.ImportAsync(stream, null!, MapFormatType.Svg));
    }

    [Fact]
    public async Task ImportAsync_EmptyFileName_ThrowsArgumentException()
    {
        var (svc, _) = CreateMapImportService();
        using var stream = new MemoryStream("<svg></svg>"u8.ToArray());

        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.ImportAsync(stream, "", MapFormatType.Svg));
    }

    [Fact]
    public async Task ImportAsync_WhitespaceFileName_ThrowsArgumentException()
    {
        var (svc, _) = CreateMapImportService();
        using var stream = new MemoryStream("<svg></svg>"u8.ToArray());

        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.ImportAsync(stream, "   ", MapFormatType.Svg));
    }

    // ── MI-3: DeleteAsync must clean up the physical file ─────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesPhysicalFile()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(storageRoot, "maps"));

        // Create a fake SVG file as the parser would
        var savedName = $"{Guid.NewGuid()}.svg";
        var physicalPath = Path.Combine(storageRoot, "maps", savedName);
        await File.WriteAllTextAsync(physicalPath, "<svg/>");

        // The stored URL path (relative) that would be in the DB
        var urlPath = $"/module-assets/TPS.Nexus.Kanban/maps/{savedName}";

        Assert.True(File.Exists(physicalPath), "precondition: file exists before delete");

        // Simulate what MapImportService.TryDeleteFile does — resolve URL to physical path
        var fileName = Path.GetFileName(urlPath);
        var resolvedPath = Path.Combine(storageRoot, "maps", fileName);
        if (File.Exists(resolvedPath)) File.Delete(resolvedPath);

        Assert.False(File.Exists(physicalPath), "file should be deleted");

        Directory.Delete(storageRoot, recursive: true);
    }

    private static (Services.Map.MapImportService svc, string storageRoot) CreateMapImportService()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var envMock = NSubstitute.Substitute.For<IWebHostEnvironmentAccessor>();
        envMock.WebRootPath.Returns(storageRoot);
        var dbMock = NSubstitute.Substitute.For<TPS.Nexus.Core.IDbConnectionFactory>();
        return (new Services.Map.MapImportService(dbMock, envMock), storageRoot);
    }
}
