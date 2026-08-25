using MediaGrab.Domain.Enums;

namespace MediaGrab.Application.Models;

/// <summary>
/// Everything a provider was able to (legitimately) learn about a URL
/// during analysis. Only fields a provider can actually determine are
/// populated - the rest are left null rather than guessed.
/// </summary>
public class MediaMetadata
{
    public string Title { get; init; } = string.Empty;

    public string? ThumbnailUrl { get; init; }

    public TimeSpan? Duration { get; init; }

    public MediaFileType MediaType { get; init; } = MediaFileType.Unknown;

    /// <summary>Best-effort size estimate for the default/only format, when known up front.</summary>
    public long? EstimatedSizeBytes { get; init; }

    public IReadOnlyList<MediaFormat> Formats { get; init; } = Array.Empty<MediaFormat>();
}
