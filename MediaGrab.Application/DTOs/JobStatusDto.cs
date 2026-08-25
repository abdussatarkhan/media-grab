namespace MediaGrab.Application.DTOs;

/// <summary>
/// Response for GET /api/media/status/{id}. Deliberately excludes any
/// internal server path - only a job id, filename, and size are exposed.
/// </summary>
public class JobStatusDto
{
    public Guid JobId { get; set; }

    public string Status { get; set; } = string.Empty;

    public int Progress { get; set; }

    public string? ErrorMessage { get; set; }

    public string? FileName { get; set; }

    public long? FileSizeBytes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }

    public bool CanDownload { get; set; }
}
