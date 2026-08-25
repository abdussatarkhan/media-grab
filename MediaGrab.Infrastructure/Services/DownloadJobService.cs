using System.Text.Json;
using MediaGrab.Application.DTOs;
using MediaGrab.Application.Interfaces;
using MediaGrab.Application.Models;
using MediaGrab.Domain.Entities;
using MediaGrab.Domain.Enums;
using MediaGrab.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MediaGrab.Infrastructure.Services;

/// <summary>
/// Orchestrates the full job lifecycle: analyze -> queue -> (background
/// worker processes) -> completed/failed -> download -> cleanup.
/// Contains no HTTP/controller concerns so it can be unit tested and
/// reused from the background worker.
/// </summary>
public class DownloadJobService : IDownloadJobService
{
    private readonly MediaGrabDbContext _dbContext;
    private readonly IUrlValidator _urlValidator;
    private readonly IProviderRegistry _providerRegistry;
    private readonly IDownloadQueue _queue;
    private readonly ILogger<DownloadJobService> _logger;

    public DownloadJobService(
        MediaGrabDbContext dbContext,
        IUrlValidator urlValidator,
        IProviderRegistry providerRegistry,
        IDownloadQueue queue,
        ILogger<DownloadJobService> logger)
    {
        _dbContext = dbContext;
        _urlValidator = urlValidator;
        _providerRegistry = providerRegistry;
        _queue = queue;
        _logger = logger;
    }

    public async Task<AnalyzeResponseDto> CreateAnalysisJobAsync(string userId, string rawUrl, CancellationToken cancellationToken = default)
    {
        var validation = _urlValidator.Validate(rawUrl);

        if (!validation.IsValid || validation.NormalizedUri is null)
        {
            _logger.LogInformation("Rejected analyze request for user {UserId}: {Reason}", userId, validation.ErrorMessage);

            return new AnalyzeResponseDto
            {
                IsSupported = false,
                Status = DownloadJobStatus.Rejected.ToString(),
                Message = validation.ErrorMessage
            };
        }

        var provider = await _providerRegistry.FindProviderAsync(validation.NormalizedUri, cancellationToken);

        if (provider is null)
        {
            var rejectedJob = new DownloadJob
            {
                UserId = userId,
                SourceUrl = validation.NormalizedUri.ToString(),
                Status = DownloadJobStatus.Rejected,
                ErrorMessage = "No authorized provider currently supports this URL."
            };

            _dbContext.DownloadJobs.Add(rejectedJob);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new AnalyzeResponseDto
            {
                JobId = rejectedJob.Id,
                Status = rejectedJob.Status.ToString(),
                IsSupported = false,
                Message = rejectedJob.ErrorMessage
            };
        }

        var job = new DownloadJob
        {
            UserId = userId,
            SourceUrl = validation.NormalizedUri.ToString(),
            Status = DownloadJobStatus.Analyzing
        };

        _dbContext.DownloadJobs.Add(job);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var analysis = await provider.AnalyzeAsync(validation.NormalizedUri, cancellationToken);

        if (!analysis.Success || analysis.Data is null)
        {
            job.Status = DownloadJobStatus.Failed;
            job.ErrorMessage = analysis.ErrorMessage ?? "Analysis failed.";
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new AnalyzeResponseDto
            {
                JobId = job.Id,
                Status = job.Status.ToString(),
                ProviderName = provider.DisplayName,
                IsSupported = true,
                Message = job.ErrorMessage
            };
        }

        var metadata = analysis.Data;
        job.Status = DownloadJobStatus.Ready;
        job.MetadataJson = JsonSerializer.Serialize(metadata);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Analyzed job {JobId} for user {UserId} via provider {ProviderKey}.", job.Id, userId, provider.ProviderKey);

        return new AnalyzeResponseDto
        {
            JobId = job.Id,
            Status = job.Status.ToString(),
            ProviderName = provider.DisplayName,
            IsSupported = true,
            Message = "Analysis complete. Select a format to start the download.",
            Title = metadata.Title,
            ThumbnailUrl = metadata.ThumbnailUrl,
            DurationSeconds = metadata.Duration?.TotalSeconds,
            MediaType = metadata.MediaType.ToString(),
            EstimatedSizeBytes = metadata.EstimatedSizeBytes,
            Formats = metadata.Formats.Select(f => new MediaFormatDto
            {
                FormatId = f.FormatId,
                Label = f.Label,
                Extension = f.Extension,
                EstimatedSizeBytes = f.EstimatedSizeBytes
            }).ToList()
        };
    }

    public async Task<JobStatusDto?> GetJobStatusAsync(Guid jobId, string userId, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.DownloadJobs
            .AsNoTracking()
            .Include(j => j.MediaFiles)
            .FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId, cancellationToken);

        return job is null ? null : ToStatusDto(job);
    }

    public async Task<JobStatusDto?> StartDownloadAsync(Guid jobId, string userId, string? formatId, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.DownloadJobs.FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId, cancellationToken);

        if (job is null)
        {
            return null;
        }

        if (job.Status is not (DownloadJobStatus.Ready or DownloadJobStatus.Failed))
        {
            return ToStatusDto(job);
        }

        job.SelectedFormatId = string.IsNullOrWhiteSpace(formatId) ? "original" : formatId;
        job.ErrorMessage = null;
        job.Progress = 0;
        job.StartedAtUtc = null; // clear any stale value from a prior failed attempt before this retry

        if (!_queue.TryEnqueue(job.Id))
        {
            job.Status = DownloadJobStatus.Failed;
            job.ErrorMessage = "The download queue is currently full. Please try again shortly.";
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToStatusDto(job);
        }

        job.Status = DownloadJobStatus.Queued;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Queued job {JobId} for background processing.", job.Id);

        return ToStatusDto(job);
    }

    public async Task<bool> CancelJobAsync(Guid jobId, string userId, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.DownloadJobs.FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId, cancellationToken);

        if (job is null)
        {
            return false;
        }

        if (job.Status is DownloadJobStatus.Completed or DownloadJobStatus.Cancelled or DownloadJobStatus.Expired)
        {
            return false;
        }

        job.Status = DownloadJobStatus.Cancelled;
        job.CompletedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cancelled job {JobId} at user request.", job.Id);
        return true;
    }

    public async Task<CompletedFileInfoDto?> GetCompletedFileAsync(Guid jobId, string userId, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.DownloadJobs
            .AsNoTracking()
            .Include(j => j.MediaFiles)
            .FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId, cancellationToken);

        if (job is null || job.Status != DownloadJobStatus.Completed)
        {
            return null;
        }

        var file = job.MediaFiles.FirstOrDefault(f => f.IsAvailable && !string.IsNullOrEmpty(f.StoragePath));
        if (file is null)
        {
            return null;
        }

        return new CompletedFileInfoDto
        {
            JobId = job.Id,
            StorageKey = file.StoragePath!,
            FileName = file.FileName,
            ContentType = file.ContentType ?? "application/octet-stream",
            SizeBytes = file.SizeInBytes
        };
    }

    public async Task<IReadOnlyList<JobStatusDto>> GetRecentJobsAsync(string userId, int take = 20, CancellationToken cancellationToken = default)
    {
        var jobs = await _dbContext.DownloadJobs
            .AsNoTracking()
            .Include(j => j.MediaFiles)
            .Where(j => j.UserId == userId)
            .OrderByDescending(j => j.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(cancellationToken);

        return jobs.Select(ToStatusDto).ToList();
    }

    public async Task MarkFileConsumedAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.DownloadJobs
            .Include(j => j.MediaFiles)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        if (job is null)
        {
            return;
        }

        foreach (var file in job.MediaFiles.Where(f => f.IsAvailable))
        {
            file.IsAvailable = false;
        }

        // The file has been delivered and its temp copy removed - move
        // the job to Expired so it reads accurately ("no longer
        // available for re-download") rather than still Completed.
        if (job.Status == DownloadJobStatus.Completed)
        {
            job.Status = DownloadJobStatus.Expired;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static JobStatusDto ToStatusDto(DownloadJob job)
    {
        var file = job.MediaFiles.FirstOrDefault(f => f.IsAvailable);

        return new JobStatusDto
        {
            JobId = job.Id,
            Status = job.Status.ToString(),
            Progress = job.Progress,
            ErrorMessage = job.ErrorMessage,
            FileName = file?.FileName,
            FileSizeBytes = file?.SizeInBytes,
            CreatedAtUtc = job.CreatedAtUtc,
            StartedAtUtc = job.StartedAtUtc,
            CompletedAtUtc = job.CompletedAtUtc,
            ExpiresAtUtc = job.ExpiresAtUtc,
            CanDownload = job.Status == DownloadJobStatus.Completed && file is not null
        };
    }
}
