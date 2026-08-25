using MediaGrab.Application.Options;
using MediaGrab.Infrastructure.Http;
using MediaGrab.Infrastructure.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace MediaGrab.Tests.Providers;

public class DirectFileProviderTests
{
    private static DirectFileProvider CreateProvider() => new(
        new SsrfSafeHttpClientFactory(),
        Options.Create(new MediaGrabOptions()),
        NullLogger<DirectFileProvider>.Instance);

    [Theory]
    [InlineData("https://example.com/video.mp4", true)]
    [InlineData("https://example.com/song.mp3", true)]
    [InlineData("https://example.com/doc.pdf", true)]
    [InlineData("https://example.com/page.html", false)]
    [InlineData("https://example.com/", false)]
    public async Task CanHandleAsync_MatchesKnownFileExtensionsOnly(string url, bool expected)
    {
        var provider = CreateProvider();

        var result = await provider.CanHandleAsync(new Uri(url));

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task AnalyzeAsync_FailsGracefully_ForUnreachableHost()
    {
        var provider = CreateProvider();

        // A .invalid TLD is guaranteed by RFC 2606 to never resolve -
        // this exercises the failure path without making a real network
        // call to anything that could actually respond.
        var result = await provider.AnalyzeAsync(new Uri("https://this-host-does-not-exist.invalid/file.mp4"));

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }
}
