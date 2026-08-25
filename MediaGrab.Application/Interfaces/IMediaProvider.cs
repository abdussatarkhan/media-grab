using MediaGrab.Application.Models;

namespace MediaGrab.Application.Interfaces;

/// <summary>
/// Contract every media provider integration must implement. A provider
/// represents a single authorized source (e.g. "direct HTTP file links").
/// Implementations must only access content the requesting user is
/// legitimately authorized to access, and must never bypass DRM,
/// authentication, paywalls, or platform access controls.
/// </summary>
public interface IMediaProvider
{
    /// <summary>Stable, unique key matching SupportedProvider.Key in the database.</summary>
    string ProviderKey { get; }

    string DisplayName { get; }

    /// <summary>Fast, cheap check - does this provider know how to handle the given URL?</summary>
    Task<bool> CanHandleAsync(Uri uri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyze the URL and return whatever metadata/format options can be
    /// legitimately determined, without downloading the full resource
    /// unless technically unavoidable.
    /// </summary>
    Task<ProviderResult<MediaMetadata>> AnalyzeAsync(Uri uri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stream the selected format's bytes into <paramref name="destination"/>.
    /// Implementations must use async I/O, honor <paramref name="cancellationToken"/>
    /// promptly, and must never buffer the entire file in memory.
    /// </summary>
    Task<DownloadResult> DownloadAsync(
        Uri uri,
        MediaFormat format,
        Stream destination,
        IProgress<int>? progressPercent,
        CancellationToken cancellationToken = default);
}
