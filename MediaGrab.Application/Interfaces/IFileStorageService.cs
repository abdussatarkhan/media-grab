namespace MediaGrab.Application.Interfaces;

/// <summary>
/// Abstraction over where temporary downloaded files live. The local
/// filesystem implementation ships first; a cloud object-storage
/// implementation can be added later purely inside Infrastructure
/// without any caller needing to change.
///
/// Every method takes/returns an opaque "storage key" - never a raw
/// filesystem path - so callers (controllers, job service) can't leak
/// or manipulate server paths.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Reserve a new, unique storage key for a job's output file. The
    /// caller-supplied file name is sanitized and only used to derive a
    /// safe extension/display name - it is never trusted as a path.
    /// </summary>
    string CreateStorageKey(Guid jobId, string suggestedFileName);

    /// <summary>Open a writable stream for the given storage key, creating the target directory if needed.</summary>
    Task<Stream> OpenWriteAsync(string storageKey, CancellationToken cancellationToken = default);

    /// <summary>Open a readable stream for the given storage key. Throws FileNotFoundException if missing.</summary>
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default);

    Task<long?> GetSizeAsync(string storageKey, CancellationToken cancellationToken = default);

    /// <summary>Delete the file for a storage key. Safe to call even if it does not exist.</summary>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
