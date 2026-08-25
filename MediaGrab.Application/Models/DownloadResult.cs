namespace MediaGrab.Application.Models;

/// <summary>Outcome of a provider actually transferring a file's bytes to storage.</summary>
public class DownloadResult
{
    public bool Success { get; init; }

    public string? ErrorMessage { get; init; }

    public string? SuggestedFileName { get; init; }

    public long SizeBytes { get; init; }

    public string? ContentType { get; init; }

    public static DownloadResult Ok(string fileName, long sizeBytes, string? contentType) => new()
    {
        Success = true,
        SuggestedFileName = fileName,
        SizeBytes = sizeBytes,
        ContentType = contentType
    };

    public static DownloadResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}
