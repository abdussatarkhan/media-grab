using MediaGrab.Application.Options;
using MediaGrab.Infrastructure.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace MediaGrab.Tests.Storage;

public class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly LocalFileStorageService _service;

    public LocalFileStorageServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "mediagrab-tests-" + Guid.NewGuid().ToString("N"));

        var options = Options.Create(new MediaGrabOptions { TempStoragePath = _tempRoot });
        var env = new FakeHostEnvironment(_tempRoot);

        _service = new LocalFileStorageService(options, env, NullLogger<LocalFileStorageService>.Instance);
    }

    [Fact]
    public void CreateStorageKey_NeverContainsPathSeparatorsOrTraversal()
    {
        var key = _service.CreateStorageKey(Guid.NewGuid(), "../../etc/passwd");

        Assert.DoesNotContain("..", key);
        Assert.DoesNotContain("/", key);
        Assert.DoesNotContain("\\", key);
    }

    [Fact]
    public async Task WriteThenRead_RoundTripsBytes()
    {
        var key = _service.CreateStorageKey(Guid.NewGuid(), "video.mp4");
        var payload = new byte[] { 1, 2, 3, 4, 5 };

        await using (var writeStream = await _service.OpenWriteAsync(key))
        {
            await writeStream.WriteAsync(payload);
        }

        Assert.True(await _service.ExistsAsync(key));
        Assert.Equal(payload.Length, await _service.GetSizeAsync(key));

        await using var readStream = await _service.OpenReadAsync(key);
        using var memoryStream = new MemoryStream();
        await readStream.CopyToAsync(memoryStream);

        Assert.Equal(payload, memoryStream.ToArray());
    }

    [Fact]
    public async Task DeleteAsync_RemovesFile_AndIsSafeToCallTwice()
    {
        var key = _service.CreateStorageKey(Guid.NewGuid(), "file.pdf");
        await using (var writeStream = await _service.OpenWriteAsync(key))
        {
            await writeStream.WriteAsync(new byte[] { 9 });
        }

        await _service.DeleteAsync(key);
        Assert.False(await _service.ExistsAsync(key));

        // Deleting an already-deleted key must not throw.
        await _service.DeleteAsync(key);
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("..\\escape.txt")]
    [InlineData("sub/dir.txt")]
    [InlineData("")]
    public async Task OpenReadAsync_RejectsMaliciousStorageKeys(string maliciousKey)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.OpenReadAsync(maliciousKey));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            ContentRootFileProvider = null!;
        }

        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "MediaGrab.Tests";
        public string ContentRootPath { get; set; }
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
    }
}
