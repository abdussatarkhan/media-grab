using System.Text;

namespace MediaGrab.Infrastructure.Utilities;

/// <summary>
/// Strips a user- or remote-server-supplied file name down to something
/// safe to display, store as metadata, and send back in a
/// Content-Disposition header. Never used to construct an actual
/// filesystem path - storage always uses a generated GUID-based key.
/// </summary>
public static class FileNameSanitizer
{
    private const int MaxLength = 150;

    public static string Sanitize(string? candidate, string fallback = "download")
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return fallback;
        }

        // Strip any path segments - defends against "../../etc/passwd"
        // style input regardless of separator style.
        var name = candidate.Replace('\\', '/');
        var lastSlash = name.LastIndexOf('/');
        if (lastSlash >= 0)
        {
            name = name[(lastSlash + 1)..];
        }

        name = Uri.UnescapeDataString(name);

        var invalid = Path_GetInvalidFileNameChars();
        var builder = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            builder.Append(invalid.Contains(ch) ? '_' : ch);
        }

        var cleaned = builder.ToString().Trim().Trim('.', ' ');

        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return fallback;
        }

        if (cleaned.Length > MaxLength)
        {
            var ext = Path.GetExtension(cleaned);
            var stem = Path.GetFileNameWithoutExtension(cleaned);
            var keep = Math.Max(1, MaxLength - ext.Length);
            cleaned = stem[..Math.Min(stem.Length, keep)] + ext;
        }

        return cleaned;
    }

    public static string SafeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var trimmed = extension.Trim().TrimStart('.').ToLowerInvariant();

        if (trimmed.Length == 0 || trimmed.Length > 10 || !trimmed.All(char.IsLetterOrDigit))
        {
            return string.Empty;
        }

        return "." + trimmed;
    }

    private static char[] Path_GetInvalidFileNameChars() =>
        Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\', ':' }).Distinct().ToArray();
}
