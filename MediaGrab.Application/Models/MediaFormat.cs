namespace MediaGrab.Application.Models;

/// <summary>
/// A single downloadable format/quality option discovered during
/// analysis (e.g. "original" for a direct file link). FormatId is what
/// the client sends back in POST /api/media/download to select it.
/// </summary>
public class MediaFormat
{
    public string FormatId { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string Extension { get; init; } = string.Empty;

    public string? MimeType { get; init; }

    public long? EstimatedSizeBytes { get; init; }
}
