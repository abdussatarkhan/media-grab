using System.Security.Claims;
using MediaGrab.Application.Interfaces;
using MediaGrab.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediaGrab.Web.Controllers;

/// <summary>
/// Signed-in area, scoped strictly to the current user's own jobs -
/// IDownloadJobService filters every query by the authenticated user id,
/// so there is no risk of one user seeing another's history here.
/// </summary>
[Authorize]
public class DashboardController : Controller
{
    private readonly IDownloadJobService _jobService;

    public DashboardController(IDownloadJobService jobService)
    {
        _jobService = jobService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var jobs = await _jobService.GetRecentJobsAsync(GetUserId(), take: 10, cancellationToken);

        var model = new DashboardViewModel
        {
            RecentJobs = jobs,
            TotalDownloads = jobs.Count(j => j.Status == "Completed"),
            ActiveJobs = jobs.Count(j => j.Status is "Queued" or "Processing" or "Analyzing" or "Pending"),
            CompletedJobs = jobs.Count(j => j.Status == "Completed"),
            FailedJobs = jobs.Count(j => j.Status is "Failed" or "Rejected")
        };

        return View(model);
    }

    public async Task<IActionResult> History(CancellationToken cancellationToken)
    {
        var jobs = await _jobService.GetRecentJobsAsync(GetUserId(), take: 100, cancellationToken);

        return View(new HistoryViewModel { Jobs = jobs });
    }

    public IActionResult Settings() => View();

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
