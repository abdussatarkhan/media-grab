namespace MediaGrab.Domain.Entities;

/// <summary>
/// A lightweight, queryable audit record kept even if the underlying
/// DownloadJob and its files are later purged, so the user's "/history"
/// page and the admin audit trail still have something to show.
/// </summary>
public class DownloadHistory : BaseEntity
{
    public Guid DownloadJobId { get; set; }

    public DownloadJob? DownloadJob { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string SourceUrl { get; set; } = string.Empty;

    public string FinalStatus { get; set; } = string.Empty;

    public DateTime AttemptedAtUtc { get; set; } = DateTime.UtcNow;
}
