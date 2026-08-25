using MediaGrab.Application.Interfaces;
using MediaGrab.Application.Options;
using MediaGrab.Domain.Enums;
using MediaGrab.Infrastructure.Database;
using MediaGrab.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MediaGrab.Web.Controllers.Admin;

/// <summary>
/// Administrative area. Every action requires the "Administrator" role
/// via the "RequireAdministrator" policy (configured in Program.cs) -
/// normal authenticated users get a 403, not just a hidden nav link.
/// Read-only reporting queries use the DbContext directly (AsNoTracking)
/// since this is simple aggregation, not business logic that needs to be
/// reused elsewhere.
/// </summary>
[Authorize(Policy = "RequireAdministrator")]
[Route("admin")]
public class AdminController : Controller
{
    private readonly MediaGrabDbContext _dbContext;
    private readonly IDownloadQueue _queue;
    private readonly MediaGrabOptions _options;

    public AdminController(MediaGrabDbContext dbContext, IDownloadQueue queue, IOptions<MediaGrabOptions> options)
    {
        _dbContext = dbContext;
        _queue = queue;
        _options = options.Value;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var jobs = _dbContext.DownloadJobs.AsNoTracking();

        var model = new AdminOverviewViewModel
        {
            TotalUsers = await _dbContext.Users.CountAsync(cancellationToken),
            TotalJobs = await jobs.CountAsync(cancellationToken),
            ActiveJobs = await jobs.CountAsync(j => j.Status == DownloadJobStatus.Queued || j.Status == DownloadJobStatus.Processing || j.Status == DownloadJobStatus.Analyzing, cancellationToken),
            CompletedJobs = await jobs.CountAsync(j => j.Status == DownloadJobStatus.Completed, cancellationToken),
            FailedJobs = await jobs.CountAsync(j => j.Status == DownloadJobStatus.Failed || j.Status == DownloadJobStatus.Rejected, cancellationToken),
            TotalBytesServed = await _dbContext.MediaFiles.AsNoTracking().SumAsync(f => (long?)f.SizeInBytes ?? 0, cancellationToken),
            ActiveProviders = await _dbContext.SupportedProviders.AsNoTracking().CountAsync(p => p.Status == ProviderStatus.Active, cancellationToken)
        };

        return View(model);
    }

    [HttpGet("downloads")]
    public async Task<IActionResult> Downloads(CancellationToken cancellationToken)
    {
        var rows = await _dbContext.DownloadJobs
            .AsNoTracking()
            .OrderByDescending(j => j.CreatedAtUtc)
            .Take(200)
            .Select(j => new AdminDownloadRowViewModel
            {
                JobId = j.Id,
                UserEmail = j.UserId,
                Status = j.Status.ToString(),
                SourceUrl = j.SourceUrl,
                CreatedAtUtc = j.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        // Resolve emails in a second pass to avoid a join across the
        // Identity schema inside the projection above.
        var userIds = rows.Select(r => r.UserEmail).Distinct().ToList();
        var emailsByUserId = await _dbContext.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.Id, cancellationToken);

        foreach (var row in rows)
        {
            row.UserEmail = emailsByUserId.TryGetValue(row.UserEmail, out var email) ? email : row.UserEmail;
        }

        return View(rows);
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users(CancellationToken cancellationToken)
    {
        var jobCounts = await _dbContext.DownloadJobs
            .AsNoTracking()
            .GroupBy(j => j.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, cancellationToken);

        var users = await _dbContext.Users.AsNoTracking().OrderByDescending(u => u.CreatedAtUtc).Take(200).ToListAsync(cancellationToken);

        var rows = users.Select(u => new AdminUserRowViewModel
        {
            Id = u.Id,
            Email = u.Email ?? u.UserName ?? u.Id,
            CreatedAtUtc = u.CreatedAtUtc,
            JobCount = jobCounts.TryGetValue(u.Id, out var count) ? count : 0
        }).ToList();

        return View(rows);
    }

    [HttpGet("providers")]
    public async Task<IActionResult> Providers(CancellationToken cancellationToken)
    {
        var rows = await _dbContext.SupportedProviders
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new AdminProviderRowViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Key = p.Key,
                Status = p.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        return View(rows);
    }

    [HttpGet("system")]
    public IActionResult System()
    {
        var model = new AdminSystemViewModel
        {
            MaxConcurrentJobs = _options.MaxConcurrentJobs,
            MaxQueuedJobs = _options.MaxQueuedJobs,
            MaxFileSizeBytes = _options.MaxFileSizeBytes,
            FileRetentionMinutes = _options.FileRetentionMinutes,
            ApproximateQueueDepth = _queue.ApproximateCount
        };

        return View(model);
    }
}
