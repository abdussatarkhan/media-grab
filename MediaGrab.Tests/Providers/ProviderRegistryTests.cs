using MediaGrab.Application.Interfaces;
using MediaGrab.Application.Options;
using MediaGrab.Infrastructure.Http;
using MediaGrab.Infrastructure.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace MediaGrab.Tests.Providers;

public class ProviderRegistryTests
{
    private static DirectFileProvider CreateDirectFileProvider() => new(
        new SsrfSafeHttpClientFactory(),
        Options.Create(new MediaGrabOptions()),
        NullLogger<DirectFileProvider>.Instance);

    [Fact]
    public async Task FindProviderAsync_ReturnsMatchingProvider_ForDirectFileUrl()
    {
        var registry = new ProviderRegistry(
            new IMediaProvider[] { CreateDirectFileProvider() },
            NullLogger<ProviderRegistry>.Instance);

        var match = await registry.FindProviderAsync(new Uri("https://example.com/movie.mp4"));

        Assert.NotNull(match);
        Assert.Equal("direct-file", match!.ProviderKey);
    }

    [Fact]
    public async Task FindProviderAsync_ReturnsNull_WhenNoProviderMatches()
    {
        var registry = new ProviderRegistry(
            new IMediaProvider[] { CreateDirectFileProvider() },
            NullLogger<ProviderRegistry>.Instance);

        var match = await registry.FindProviderAsync(new Uri("https://example.com/some/page"));

        Assert.Null(match);
    }

    [Fact]
    public void GetAllProviders_ReturnsEveryRegisteredProvider()
    {
        var registry = new ProviderRegistry(
            new IMediaProvider[] { CreateDirectFileProvider() },
            NullLogger<ProviderRegistry>.Instance);

        Assert.Single(registry.GetAllProviders());
    }
}
