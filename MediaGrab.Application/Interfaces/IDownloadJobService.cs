using MediaGrab.Application.DTOs;

namespace MediaGrab.Application.Interfaces;

/// <summary>
/// Application-level orchestration for creating and querying download
/// jobs. Controllers and Razor Pages depend on this, not on the DbContext
/// directly, so business logic never leaks into the presentation layer.
/// </summary>
public interface IDownloadJobService
{
    /// <summary>Validate + analyze a URL, creating a job row that captures the outcome either way.</summary>
    Task<AnalyzeResponseDto> CreateAnalysisJobAsync(string userId, string rawUrl, CancellationToken cancellationToken = default);

    Task<JobStatusDto?> GetJobStatusAsync(Guid jobId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// User has picked a format for a previously analyzed job; queue it
    /// for background processing. Returns null if the job does not exist
    /// or does not belong to the user.
    /// </summary>
    Task<JobStatusDto?> StartDownloadAsync(Guid jobId, string userId, string? formatId, CancellationToken cancellationToken = default);

    Task<bool> CancelJobAsync(Guid jobId, string userId, CancellationToken cancellationToken = default);

    /// <summary>Resolve the on-disk storage key + safe file info for a completed job's download, scoped to the owning user.</summary>
    Task<CompletedFileInfoDto?> GetCompletedFileAsync(Guid jobId, string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobStatusDto>> GetRecentJobsAsync(string userId, int take = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called by the file-streaming endpoint once a completed job's file
    /// has finished being delivered and its temporary copy deleted, so
    /// status polls afterward accurately reflect that it's gone rather
    /// than still claiming CanDownload.
    /// </summary>
    Task MarkFileConsumedAsync(Guid jobId, CancellationToken cancellationToken = default);
}
