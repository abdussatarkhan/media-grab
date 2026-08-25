namespace MediaGrab.Application.Options;

/// <summary>
/// Configurable limits and behavior for Phase 2, bound from the
/// "MediaGrab" section of appsettings.json / environment variables.
/// Nothing production-sensitive is hard-coded in application code.
/// </summary>
public class MediaGrabOptions
{
    public const string SectionName = "MediaGrab";

    public string SiteName { get; set; } = "MediaGrab";

    public int MaxUrlLength { get; set; } = 2048;

    /// <summary>Hard ceiling on a single file's size, in bytes. Analyses/downloads exceeding this are rejected.</summary>
    public long MaxFileSizeBytes { get; set; } = 2L * 1024 * 1024 * 1024; // 2 GB

    /// <summary>Maximum time a single download job is allowed to run before being force-failed.</summary>
    public int MaxProcessingTimeSeconds { get; set; } = 15 * 60;

    /// <summary>Maximum number of jobs the background worker(s) will process at the same time.</summary>
    public int MaxConcurrentJobs { get; set; } = 4;

    /// <summary>Bounded in-memory queue capacity; beyond this, new job submissions are rejected (503) rather than growing unbounded.</summary>
    public int MaxQueuedJobs { get; set; } = 200;

    /// <summary>How long a completed file is kept before automatic cleanup, if never downloaded.</summary>
    public int FileRetentionMinutes { get; set; } = 60;

    /// <summary>How often the cleanup background service scans for expired/orphaned jobs and files.</summary>
    public int CleanupIntervalSeconds { get; set; } = 120;

    /// <summary>Relative (to content root) or absolute path for temporary downloaded files.</summary>
    public string TempStoragePath { get; set; } = "TempFiles";

    public RateLimitOptions RateLimits { get; set; } = new();
}

public class RateLimitOptions
{
    public int AnalyzePerMinute { get; set; } = 20;

    public int DownloadStartPerMinute { get; set; } = 10;

    public int FileDownloadPerMinute { get; set; } = 30;

    public int GeneralApiPerMinute { get; set; } = 120;

    public int AuthAttemptsPer5Minutes { get; set; } = 10;
}
