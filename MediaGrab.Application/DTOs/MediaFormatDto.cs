namespace MediaGrab.Application.DTOs;

public class MediaFormatDto
{
    public string FormatId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string? Extension { get; set; }

    public long? EstimatedSizeBytes { get; set; }
}
