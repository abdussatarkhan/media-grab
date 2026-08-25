using MediaGrab.Domain.Enums;

namespace MediaGrab.Domain.Entities;

/// <summary>
/// A single user request to analyze/download a URL. This is the central
/// entity Phase 2's processing pipeline will operate on.
/// </summary>
public class DownloadJob : BaseEntity
{
    public string UserId { get; set; } = string.Empty; // FK to ASP.NET Core Identity user (AspNetUsers.Id)

    public string SourceUrl { get; set; } = string.Empty;

    public Guid? SupportedProviderId { get; set; }

    public SupportedProvider? SupportedProvider { get; set; }

    public DownloadJobStatus Status { get; set; } = DownloadJobStatus.Pending;

    public string? ErrorMessage { get; set; }

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>When the completed file's temporary copy is eligible for automatic cleanup.</summary>
    public DateTime? ExpiresAtUtc { get; set; }

    /// <summary>0-100. Only meaningful while Status is Processing/Downloading.</summary>
    public int Progress { get; set; }

    /// <summary>Analyzed metadata captured for display (title, duration, etc.), stored as JSON.</summary>
    public string? MetadataJson { get; set; }

    /// <summary>The FormatId (from MediaFormat) the user selected to start the download job.</summary>
    public string? SelectedFormatId { get; set; }

    public ICollection<MediaFile> MediaFiles { get; set; } = new List<MediaFile>();

    public DownloadHistory? History { get; set; }
}
