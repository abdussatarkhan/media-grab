namespace MediaGrab.Domain.Enums;

/// <summary>
/// Represents the lifecycle of a submitted download job.
/// Kept intentionally simple in Phase 1 - the download/analysis engine
/// itself is built in Phase 2, but the state machine is defined now so
/// the database schema does not need to change later.
/// </summary>
public enum DownloadJobStatus
{
    Pending = 0,
    Analyzing = 1,
    Ready = 2, // analyzed successfully; waiting for the user to pick a format and start the download
    Downloading = 3, // retained for compatibility; superseded by Processing for Phase 2's background worker
    Completed = 4,
    Failed = 5,
    Rejected = 6, // e.g. URL failed validation or provider is not authorized
    Cancelled = 7,
    Queued = 8, // Phase 2: accepted into the background download queue, waiting for a worker slot
    Processing = 9, // Phase 2: a background worker is actively downloading/transferring the file
    Expired = 10 // Phase 2: completed file's retention window passed and it was cleaned up
}
