using MediaGrab.Application.Interfaces;
using MediaGrab.Application.Options;
using MediaGrab.Domain.Enums;
using MediaGrab.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MediaGrab.Infrastructure.BackgroundServices;

/// <summary>
/// Periodic safety-net cleanup. The primary deletion path is
/// "successful download -&gt; delete" (handled by the file-download
/// endpoint immediately after a completed stream). This service is the
/// backup: it expires completed jobs nobody ever downloaded, and removes
/// any storage file left behind by a failed or cancelled job. It never
/// touches a file that is actively being streamed, because deletion for
/// a job only happens once its status is no longer Completed with an
/// available file the download endpoint could still be serving - the
/// endpoint itself performs the delete for the success path under its
/// own request scope, and this sweep only acts on jobs whose
/// ExpiresAtUtc has already passed or that ended in Failed/Cancelled,
/// never on a job mid-stream.
/// </summary>
public class FileCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MediaGrabOptions _options;
    private readonly ILogger<FileCleanupService> _logger;

    public FileCleanupService(IServiceScopeFactory scopeFactory, IOptions<MediaGrabOptions> options, ILogger<FileCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(15, _options.CleanupIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunSweepAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cleanup sweep failed.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutting down.
            }
        }
    }

    private async Task RunSweepAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaGrabDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

        var now = DateTime.UtcNow;

        // 1) Expired completed jobs nobody downloaded in time.
        var expiredJobs = await dbContext.DownloadJobs
            .Include(j => j.MediaFiles)
            .Where(j => j.Status == DownloadJobStatus.Completed && j.ExpiresAtUtc != null && j.ExpiresAtUtc < now)
            .ToListAsync(cancellationToken);

        foreach (var job in expiredJobs)
        {
            foreach (var file in job.MediaFiles.Where(f => f.IsAvailable && !string.IsNullOrEmpty(f.StoragePath)))
            {
                await storage.DeleteAsync(file.StoragePath!, cancellationToken);
                file.IsAvailable = false;
            }

            job.Status = DownloadJobStatus.Expired;
        }

        // 2) Abandoned jobs stuck in Queued/Processing far past the max
        // processing window (e.g. the app restarted mid-download).
        // Uses StartedAtUtc when available - CreatedAtUtc alone would
        // incorrectly flag a job that was legitimately retried (and thus
        // created long ago) but only recently re-queued.
        var stuckCutoff = now.AddSeconds(-Math.Max(60, _options.MaxProcessingTimeSeconds * 2));
        var stuckJobs = await dbContext.DownloadJobs
            .Where(j => (j.Status == DownloadJobStatus.Queued || j.Status == DownloadJobStatus.Processing) &&
                        (j.StartedAtUtc ?? j.CreatedAtUtc) < stuckCutoff)
            .ToListAsync(cancellationToken);

        foreach (var job in stuckJobs)
        {
            job.Status = DownloadJobStatus.Failed;
            job.ErrorMessage = "The job did not complete in time and was automatically cancelled.";
            job.CompletedAtUtc = now;
        }

        // 3) Any storage file left over from a Failed/Cancelled job.
        var abandonedFiles = await dbContext.MediaFiles
            .Include(f => f.DownloadJob)
            .Where(f => f.IsAvailable &&
                        f.DownloadJob != null &&
                        (f.DownloadJob.Status == DownloadJobStatus.Failed || f.DownloadJob.Status == DownloadJobStatus.Cancelled))
            .ToListAsync(cancellationToken);

        foreach (var file in abandonedFiles)
        {
            if (!string.IsNullOrEmpty(file.StoragePath))
            {
                await storage.DeleteAsync(file.StoragePath, cancellationToken);
            }

            file.IsAvailable = false;
        }

        if (expiredJobs.Count > 0 || stuckJobs.Count > 0 || abandonedFiles.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Cleanup sweep: expired {Expired}, stuck {Stuck}, abandoned files {Files}.",
                expiredJobs.Count, stuckJobs.Count, abandonedFiles.Count);
        }
    }
}
