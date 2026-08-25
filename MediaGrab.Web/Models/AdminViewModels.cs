namespace MediaGrab.Web.Models;

public class AdminOverviewViewModel
{
    public int TotalUsers { get; set; }

    public int TotalJobs { get; set; }

    public int ActiveJobs { get; set; }

    public int CompletedJobs { get; set; }

    public int FailedJobs { get; set; }

    public long TotalBytesServed { get; set; }

    public int ActiveProviders { get; set; }
}

public class AdminDownloadRowViewModel
{
    public Guid JobId { get; set; }

    public string UserEmail { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string SourceUrl { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}

public class AdminUserRowViewModel
{
    public string Id { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public int JobCount { get; set; }

    public bool IsAdministrator { get; set; }
}

public class AdminProviderRowViewModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}

public class AdminSystemViewModel
{
    public int MaxConcurrentJobs { get; set; }

    public int MaxQueuedJobs { get; set; }

    public long MaxFileSizeBytes { get; set; }

    public int FileRetentionMinutes { get; set; }

    public int ApproximateQueueDepth { get; set; }
}
