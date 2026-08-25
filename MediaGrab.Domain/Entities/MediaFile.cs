using MediaGrab.Domain.Enums;

namespace MediaGrab.Domain.Entities;

/// <summary>
/// A single downloadable artifact discovered/produced for a DownloadJob
/// (a job may resolve to more than one file, e.g. multiple resolutions).
/// </summary>
public class MediaFile : BaseEntity
{
    public Guid DownloadJobId { get; set; }

    public DownloadJob? DownloadJob { get; set; }

    public string FileName { get; set; } = string.Empty;

    public MediaFileType FileType { get; set; } = MediaFileType.Unknown;

    public string? Format { get; set; } // e.g. mp4, mp3, jpg

    public long? SizeInBytes { get; set; }

    public string? StoragePath { get; set; } // opaque storage key resolved by IFileStorageService - never a raw filesystem path exposed to clients

    public string? ContentType { get; set; }

    public bool IsAvailable { get; set; } = false;
}
