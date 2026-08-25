using MediaGrab.Application.DTOs;

namespace MediaGrab.Web.Models;

public class DashboardViewModel
{
    public int TotalDownloads { get; set; }

    public int ActiveJobs { get; set; }

    public int CompletedJobs { get; set; }

    public int FailedJobs { get; set; }

    public IReadOnlyList<JobStatusDto> RecentJobs { get; set; } = Array.Empty<JobStatusDto>();
}

public class HistoryViewModel
{
    public IReadOnlyList<JobStatusDto> Jobs { get; set; } = Array.Empty<JobStatusDto>();
}
