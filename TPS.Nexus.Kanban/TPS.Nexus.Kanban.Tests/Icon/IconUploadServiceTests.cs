using NSubstitute;
using TPS.Nexus.Kanban.Core.Interfaces;
using TPS.Nexus.Kanban.Services.Icon;
using Xunit;

namespace TPS.Nexus.Kanban.Tests.Icon;

public class IconUploadServiceTests
{
    private static IconUploadService CreateService(string? tempRoot = null)
    {
        var root    = tempRoot ?? Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var envMock = Substitute.For<IWebHostEnvironmentAccessor>();
        envMock.WebRootPath.Returns(root);
        return new IconUploadService(envMock);
    }

    // ── IU-1: null fileName ────────────────────────────────────────────────────

    [Fact]
    public async Task UploadAsync_NullFileName_ThrowsArgumentException()
    {
        var svc = CreateService();
        using var stream = new MemoryStream(new byte[] { 0x89, 0x50 });

        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.UploadAsync(stream, null!));
    }

    [Fact]
    public async Task UploadAsync_EmptyFileName_ThrowsArgumentException()
    {
        var svc = CreateService();
        using var stream = new MemoryStream(new byte[] { 0x89, 0x50 });

        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.UploadAsync(stream, ""));
    }

    [Fact]
    public async Task UploadAsync_WhitespaceFileName_ThrowsArgumentException()
    {
        var svc = CreateService();
        using var stream = new MemoryStream(new byte[] { 0x89, 0x50 });

        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.UploadAsync(stream, "   "));
    }

    // ── IU-2: null stream ─────────────────────────────────────────────────────

    [Fact]
    public async Task UploadAsync_NullStream_ThrowsArgumentNullException()
    {
        var svc = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => svc.UploadAsync(null!, "icon.png"));
    }

    // ── Extension validation ───────────────────────────────────────────────────

    [Theory]
    [InlineData("icon.exe")]
    [InlineData("icon.gif")]
    [InlineData("icon.bmp")]
    [InlineData("icon.webp")]
    public async Task UploadAsync_UnsupportedExtension_ThrowsInvalidOperation(string fileName)
    {
        var svc = CreateService();
        using var stream = new MemoryStream(new byte[] { 0x00 });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.UploadAsync(stream, fileName));
    }

    // ── Happy path: supported extensions return correct URL prefix ────────────

    [Theory]
    [InlineData("logo.png")]
    [InlineData("logo.jpg")]
    [InlineData("logo.jpeg")]
    [InlineData("logo.svg")]
    public async Task UploadAsync_SupportedExtension_ReturnsModuleRelativeUrl(string fileName)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var svc      = CreateService(tempRoot);
        using var stream = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        var url = await svc.UploadAsync(stream, fileName);

        Assert.StartsWith("/module-assets/TPS.Nexus.Kanban/images/equipment-icons/", url);
        Assert.EndsWith(Path.GetExtension(fileName), url);

        Directory.Delete(tempRoot, recursive: true);
    }
}
