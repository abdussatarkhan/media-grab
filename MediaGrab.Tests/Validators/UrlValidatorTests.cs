using MediaGrab.Infrastructure.Validators;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MediaGrab.Tests.Validators;

public class UrlValidatorTests
{
    private readonly UrlValidator _validator = new(NullLogger<UrlValidator>.Instance);

    [Theory]
    [InlineData("https://example.com/file.mp4")]
    [InlineData("http://example.com/path?query=1")]
    public void Validate_AcceptsWellFormedHttpUrls(string url)
    {
        var result = _validator.Validate(url);

        Assert.True(result.IsValid);
        Assert.NotNull(result.NormalizedUri);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("not a url")]
    [InlineData("ftp://example.com/file.zip")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    public void Validate_RejectsMalformedOrDisallowedSchemes(string? url)
    {
        var result = _validator.Validate(url);

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    [Theory]
    [InlineData("http://localhost/admin")]
    [InlineData("http://127.0.0.1/secret")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://10.0.0.5/internal")]
    [InlineData("http://192.168.1.1/router")]
    [InlineData("http://[::1]/")]
    public void Validate_RejectsPrivateAndInternalTargets(string url)
    {
        var result = _validator.Validate(url);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_RejectsUrlsLongerThanMaxLength()
    {
        var longUrl = "https://example.com/" + new string('a', 3000);

        var result = _validator.Validate(longUrl);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_NormalizesDefaultHttpsPort()
    {
        var result = _validator.Validate("https://example.com:443/file.mp4");

        Assert.True(result.IsValid);
        Assert.DoesNotContain(":443", result.NormalizedUri!.ToString());
    }
}
