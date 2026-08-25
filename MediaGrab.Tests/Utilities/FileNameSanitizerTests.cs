using MediaGrab.Infrastructure.Utilities;
using Xunit;

namespace MediaGrab.Tests.Utilities;

public class FileNameSanitizerTests
{
    [Theory]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("..\\..\\windows\\system32\\config", "config")]
    [InlineData("/absolute/path/video.mp4", "video.mp4")]
    [InlineData("normal-file.mp4", "normal-file.mp4")]
    public void Sanitize_StripsPathSegments(string input, string expected)
    {
        var result = FileNameSanitizer.Sanitize(input);

        Assert.Equal(expected, result);
        Assert.DoesNotContain("..", result);
        Assert.DoesNotContain("/", result);
        Assert.DoesNotContain("\\", result);
    }

    [Fact]
    public void Sanitize_ReturnsFallback_ForNullOrEmpty()
    {
        Assert.Equal("download", FileNameSanitizer.Sanitize(null));
        Assert.Equal("download", FileNameSanitizer.Sanitize(""));
        Assert.Equal("download", FileNameSanitizer.Sanitize("   "));
    }

    [Fact]
    public void Sanitize_TruncatesOverlyLongNames()
    {
        var longName = new string('a', 500) + ".mp4";

        var result = FileNameSanitizer.Sanitize(longName);

        Assert.True(result.Length <= 160);
        Assert.EndsWith(".mp4", result);
    }

    [Theory]
    [InlineData("mp4", ".mp4")]
    [InlineData(".mp4", ".mp4")]
    [InlineData("", "")]
    [InlineData("../etc", "")]
    [InlineData("toolongextensionvalue", "")]
    public void SafeExtension_OnlyAllowsShortAlphanumericExtensions(string input, string expected)
    {
        Assert.Equal(expected, FileNameSanitizer.SafeExtension(input));
    }
}
