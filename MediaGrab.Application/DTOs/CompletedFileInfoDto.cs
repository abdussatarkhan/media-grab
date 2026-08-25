namespace MediaGrab.Application.DTOs;

/// <summary>
/// Everything the file-streaming endpoint needs to serve a completed
/// job's file. StorageKey is an opaque identifier resolved by
/// IFileStorageService - it is never a raw filesystem path and is never
/// serialized back to the client.
/// </summary>
public class CompletedFileInfoDto
{
    public Guid JobId { get; set; }

    public string StorageKey { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";

    public long? SizeBytes { get; set; }
}
