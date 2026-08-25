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
/// Consumes job ids from IDownloadQueue and performs the actual
/// provider download, bounded to MediaGrab:MaxConcurrentJobs concurrent
/// transfers. Each job gets its own DI scope (a fresh DbContext) and is
/// subject to MediaGrab:MaxProcessingTimeSeconds - if a provider hangs,
/// the job is force-failed rather than blocking a worker slot forever.
/// </summary>
public class DownloadProcessingWorker : BackgroundService
{
    private readonly IDownloadQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MediaGrabOptions _options;
    private readonly ILogger<DownloadProcessingWorker> _logger;
    private readonly SemaphoreSlim _concurrencyLimiter;

    public DownloadProcessingWorker(
        IDownloadQueue queue,
        IServiceScopeFactory scopeFactory,
        IOptions<MediaGrabOptions> options,
        ILogger<DownloadProcessingWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        _concurrencyLimiter = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrentJobs));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var jobId in _queue.DequeueAllAsync(stoppingToken))
        {
            await _concurrencyLimiter.WaitAsync(stoppingToken);

            // Fire-and-forget per job, bounded by the semaphore above.
            // Deliberately not awaited so the reader loop can keep pulling
            // new jobs up to MaxConcurrentJobs in flight at once.
            _ = ProcessJobSafelyAsync(jobId, stoppingToken);
        }
    }

    private async Task ProcessJobSafelyAsync(Guid jobId, CancellationToken stoppingToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(30, _options.MaxProcessingTimeSeconds)));

            await ProcessJobAsync(jobId, timeoutCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error processing job {JobId}.", jobId);
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    private async Task ProcessJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaGrabDbContext>();
        var providerRegistry = scope.ServiceProvider.GetRequiredService<IProviderRegistry>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

        var job = await dbContext.DownloadJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
        {
            _logger.LogWarning("Job {JobId} disappeared before processing could start.", jobId);
            return;
        }

        if (job.Status != DownloadJobStatus.Queued)
        {
            // Job was cancelled or already handled between enqueue and now.
            return;
        }

        job.Status = DownloadJobStatus.Processing;
        job.StartedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        if (!Uri.TryCreate(job.SourceUrl, UriKind.Absolute, out var uri))
        {
            await FailAsync(dbContext, job, "The stored source URL is no longer valid.", cancellationToken);
            return;
        }

        var provider = await providerRegistry.FindProviderAsync(uri, cancellationToken);
        if (provider is null)
        {
            await FailAsync(dbContext, job, "No authorized provider is available to complete this download.", cancellationToken);
            return;
        }

        var analysis = await provider.AnalyzeAsync(uri, cancellationToken);
        if (!analysis.Success || analysis.Data is null)
        {
            await FailAsync(dbContext, job, analysis.ErrorMessage ?? "Re-analysis failed before download.", cancellationToken);
            return;
        }

        var format = analysis.Data.Formats.FirstOrDefault(f => f.FormatId == job.SelectedFormatId)
                     ?? analysis.Data.Formats.FirstOrDefault();

        if (format is null)
        {
            await FailAsync(dbContext, job, "The requested format is no longer available.", cancellationToken);
            return;
        }

        var storageKey = storage.CreateStorageKey(job.Id, format.Extension);
        var progress = new Progress<int>(percent =>
        {
            // Fire-and-forget percent updates - a best-effort progress
            // indicator is not worth blocking the transfer loop for.
            // Uses the worker's own scope factory (not the per-job scope,
            // which is disposed once ProcessJobAsync returns and may race
            // with this callback).
            _ = UpdateProgressAsync(_scopeFactory, job.Id, percent, cancellationToken);
        });

        try
        {
            await using var destination = await storage.OpenWriteAsync(storageKey, cancellationToken);
            var result = await provider.DownloadAsync(uri, format, destination, progress, cancellationToken);

            if (!result.Success)
            {
                await storage.DeleteAsync(storageKey, CancellationToken.None);
                await FailAsync(dbContext, job, result.ErrorMessage ?? "The download failed.", cancellationToken);
                return;
            }

            var mediaFile = new Domain.Entities.MediaFile
            {
                DownloadJobId = job.Id,
                FileName = result.SuggestedFileName ?? "download",
                Format = format.Extension,
                SizeInBytes = result.SizeBytes,
                StoragePath = storageKey,
                ContentType = result.ContentType,
                IsAvailable = true
            };

            dbContext.MediaFiles.Add(mediaFile);

            job.Status = DownloadJobStatus.Completed;
            job.Progress = 100;
            job.CompletedAtUtc = DateTime.UtcNow;
            job.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(Math.Max(1, _options.FileRetentionMinutes));

            // A job can be retried after a Failed status (StartDownloadAsync
            // allows re-queuing from Failed), so History may already exist
            // from an earlier attempt - upsert rather than blindly Add, or
            // a second insert would violate DownloadHistories' unique
            // DownloadJobId constraint.
            var existingHistory = await dbContext.DownloadHistories.FirstOrDefaultAsync(h => h.DownloadJobId == job.Id, cancellationToken);
            if (existingHistory is null)
            {
                dbContext.DownloadHistories.Add(new Domain.Entities.DownloadHistory
                {
                    DownloadJobId = job.Id,
                    UserId = job.UserId,
                    SourceUrl = job.SourceUrl,
                    FinalStatus = job.Status.ToString(),
                    AttemptedAtUtc = DateTime.UtcNow
                });
            }
            else
            {
                existingHistory.FinalStatus = job.Status.ToString();
                existingHistory.AttemptedAtUtc = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Completed job {JobId}, wrote {Bytes} bytes.", job.Id, result.SizeBytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await storage.DeleteAsync(storageKey, CancellationToken.None);
            await FailAsync(dbContext, job, "The download timed out or was cancelled.", CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while downloading job {JobId}.", job.Id);
            await storage.DeleteAsync(storageKey, CancellationToken.None);
            await FailAsync(dbContext, job, "An unexpected error occurred while downloading this file.", CancellationToken.None);
        }
    }

    private static async Task FailAsync(MediaGrabDbContext dbContext, Domain.Entities.DownloadJob job, string message, CancellationToken cancellationToken)
    {
        job.Status = DownloadJobStatus.Failed;
        job.ErrorMessage = message;
        job.CompletedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task UpdateProgressAsync(IServiceScopeFactory scopeFactory, Guid jobId, int percent, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaGrabDbContext>();

            // Minimal round-trip: only touch the Progress column.
            var job = await dbContext.DownloadJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
            if (job is not null && job.Status == DownloadJobStatus.Processing)
            {
                job.Progress = percent;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch
        {
            // Best-effort only - never let a progress update crash the transfer.
        }
    }
}
