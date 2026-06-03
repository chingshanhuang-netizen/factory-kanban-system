using NSubstitute;
using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Core.Constants;
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

        Assert.StartsWith($"{KanbanAssets.ModulePrefix}/{KanbanAssets.IconsSubdir}/", url);
        Assert.EndsWith(Path.GetExtension(fileName), url);

        Directory.Delete(tempRoot, recursive: true);
    }

    // ── IU-1 variant: file with no extension ──────────────────────────────────

    [Fact]
    public async Task UploadAsync_NoFileExtension_ThrowsInvalidOperationException()
    {
        var svc = CreateService();
        using var stream = new MemoryStream(new byte[] { 0x89, 0x50 });

        // "iconfile" has no extension — Path.GetExtension returns "" which is not allowed
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.UploadAsync(stream, "iconfile"));
    }

    // ── S-5: partial write failure must not leave orphaned file on disk ────────

    [Fact]
    public async Task UploadAsync_StreamThrowsMidCopy_DoesNotLeaveOrphanedFile()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var svc      = CreateService(tempRoot);

        await using var failingStream = new ThrowingReadStream();

        await Assert.ThrowsAsync<IOException>(
            () => svc.UploadAsync(failingStream, "icon.png"));

        // No partial files should remain in the icons directory
        var iconDir = Path.Combine(tempRoot, KanbanAssets.IconsSubdir);
        var leftover = Directory.Exists(iconDir)
            ? Directory.GetFiles(iconDir, "*.png")
            : Array.Empty<string>();

        Assert.Empty(leftover);
        if (Directory.Exists(tempRoot))
            Directory.Delete(tempRoot, recursive: true);
    }

    // Simulates a stream that always throws IOException on Read — used to trigger
    // the mid-copy failure path in IconUploadService.UploadAsync.
    private sealed class ThrowingReadStream : Stream
    {
        public override bool CanRead  => true;
        public override bool CanSeek  => false;
        public override bool CanWrite => false;
        public override long Length   => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
            => throw new IOException("Simulated read failure during upload");

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
