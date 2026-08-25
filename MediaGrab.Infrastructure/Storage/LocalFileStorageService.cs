using MediaGrab.Application.Interfaces;
using MediaGrab.Application.Options;
using MediaGrab.Infrastructure.Utilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MediaGrab.Infrastructure.Storage;

/// <summary>
/// Stores temporary downloaded files on the local disk, under a single
/// root directory (MediaGrab/TempFiles by default). Storage keys are
/// generated GUID-based file names - user/remote input is never used to
/// build a path, and every resolved path is verified to still be inside
/// the root before use, closing off path traversal even if a future
/// caller passes a malformed key.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(IOptions<MediaGrabOptions> options, IHostEnvironment environment, ILogger<LocalFileStorageService> logger)
    {
        _logger = logger;

        var configuredPath = options.Value.TempStoragePath;
        _rootPath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath);

        Directory.CreateDirectory(_rootPath);
    }

    public string CreateStorageKey(Guid jobId, string suggestedFileName)
    {
        var ext = FileNameSanitizer.SafeExtension(Path.GetExtension(FileNameSanitizer.Sanitize(suggestedFileName)));
        // Storage key format: <jobId>_<random>.ext - fully generated, never derived from untrusted text.
        return $"{jobId:N}_{Guid.NewGuid():N}{ext}";
    }

    public Task<Stream> OpenWriteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = ResolveSafePath(storageKey);
        Stream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
        return Task.FromResult(stream);
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = ResolveSafePath(storageKey);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The requested file is no longer available.");
        }

        // FileShare.Read: allow cleanup logic to inspect but callers must
        // not delete a file that's open for read (see DeleteAsync usage
        // in the download endpoint, which only deletes AFTER the stream
        // completes).
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
        return Task.FromResult(stream);
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = ResolveSafePath(storageKey);
        return Task.FromResult(File.Exists(path));
    }

    public Task<long?> GetSizeAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = ResolveSafePath(storageKey);
        if (!File.Exists(path))
        {
            return Task.FromResult<long?>(null);
        }

        return Task.FromResult<long?>(new FileInfo(path).Length);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var path = ResolveSafePath(storageKey);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            // Cleanup failures must never crash a request/background loop -
            // log and move on; the expiration sweep will retry later.
            _logger.LogWarning(ex, "Failed to delete temporary file for storage key {StorageKey}.", storageKey);
        }

        return Task.CompletedTask;
    }

    private string ResolveSafePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) || storageKey.Contains("..") ||
            storageKey.Contains('/') || storageKey.Contains('\\'))
        {
            throw new ArgumentException("Invalid storage key.", nameof(storageKey));
        }

        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, storageKey));
        var normalizedRoot = Path.GetFullPath(_rootPath + Path.DirectorySeparatorChar);

        if (!fullPath.StartsWith(normalizedRoot, StringComparison.Ordinal))
        {
            // Defense in depth - should be unreachable given the checks
            // above, but never trust a single layer for path safety.
            throw new ArgumentException("Resolved path escapes the storage root.", nameof(storageKey));
        }

        return fullPath;
    }
}
