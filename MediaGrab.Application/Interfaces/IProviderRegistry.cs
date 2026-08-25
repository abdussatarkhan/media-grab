namespace MediaGrab.Application.Interfaces;

/// <summary>
/// Central lookup used to find the right IMediaProvider for a submitted
/// URL. New providers register themselves here (typically via DI) so the
/// rest of the application never needs to know how many providers exist
/// or how they are implemented.
/// </summary>
public interface IProviderRegistry
{
    Task<IMediaProvider?> FindProviderAsync(Uri uri, CancellationToken cancellationToken = default);

    IReadOnlyList<IMediaProvider> GetAllProviders();
}
