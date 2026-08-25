namespace MediaGrab.Application.DTOs;

/// <summary>Response returned once a URL has been validated, matched to a provider, and analyzed.</summary>
public class AnalyzeResponseDto
{
    public Guid JobId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? ProviderName { get; set; }

    public bool IsSupported { get; set; }

    public string? Message { get; set; }

    public string? Title { get; set; }

    public string? ThumbnailUrl { get; set; }

    public double? DurationSeconds { get; set; }

    public string? MediaType { get; set; }

    public long? EstimatedSizeBytes { get; set; }

    public IReadOnlyList<MediaFormatDto> Formats { get; set; } = Array.Empty<MediaFormatDto>();
}
