using MediaGrab.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace MediaGrab.Infrastructure.Providers;

/// <summary>
/// Resolves a URL to the IMediaProvider that should handle it. New
/// providers register themselves via DI (see Program.cs /
/// ServiceCollectionExtensions) - this class never needs to change when a
/// new provider is added, satisfying the "no core rewrite" requirement.
/// </summary>
public class ProviderRegistry : IProviderRegistry
{
    private readonly IReadOnlyList<IMediaProvider> _providers;
    private readonly ILogger<ProviderRegistry> _logger;

    public ProviderRegistry(IEnumerable<IMediaProvider> providers, ILogger<ProviderRegistry> logger)
    {
        _providers = providers.ToList();
        _logger = logger;
    }

    public async Task<IMediaProvider?> FindProviderAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        foreach (var provider in _providers)
        {
            if (await provider.CanHandleAsync(uri, cancellationToken))
            {
                _logger.LogInformation("Provider {Provider} matched host {Host}.", provider.ProviderKey, uri.Host);
                return provider;
            }
        }

        _logger.LogInformation("No provider matched host {Host}.", uri.Host);
        return null;
    }

    public IReadOnlyList<IMediaProvider> GetAllProviders() => _providers;
}
