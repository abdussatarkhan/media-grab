using MediaGrab.Application.DTOs;
using MediaGrab.Application.Interfaces;
using MediaGrab.Application.Models;
using MediaGrab.Application.Options;
using MediaGrab.Domain.Entities;
using MediaGrab.Domain.Enums;
using MediaGrab.Infrastructure.Database;
using MediaGrab.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace MediaGrab.Tests.Services;

public class DownloadJobServiceTests
{
    private const string UserId = "user-1";

    [Fact]
    public async Task CreateAnalysisJobAsync_RejectsInvalidUrl_WithoutTouchingDatabase()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, new FakeUrlValidator(alwaysValid: false), new FakeProviderRegistry(hasProvider: true), new FakeDownloadQueue());

        var result = await service.CreateAnalysisJobAsync(UserId, "not a url");

        Assert.False(result.IsSupported);
        Assert.Equal(Guid.Empty, result.JobId);
        Assert.Equal(0, await dbContext.DownloadJobs.CountAsync());
    }

    [Fact]
    public async Task CreateAnalysisJobAsync_MarksJobRejected_WhenNoProviderMatches()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, new FakeUrlValidator(alwaysValid: true), new FakeProviderRegistry(hasProvider: false), new FakeDownloadQueue());

        var result = await service.CreateAnalysisJobAsync(UserId, "https://example.com/page");

        Assert.False(result.IsSupported);
        Assert.Equal(DownloadJobStatus.Rejected.ToString(), result.Status);

        var job = await dbContext.DownloadJobs.SingleAsync();
        Assert.Equal(DownloadJobStatus.Rejected, job.Status);
    }

    [Fact]
    public async Task CreateAnalysisJobAsync_ReturnsReadyJobWithFormats_WhenProviderAnalyzesSuccessfully()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, new FakeUrlValidator(alwaysValid: true), new FakeProviderRegistry(hasProvider: true), new FakeDownloadQueue());

        var result = await service.CreateAnalysisJobAsync(UserId, "https://example.com/video.mp4");

        Assert.True(result.IsSupported);
        Assert.Equal(DownloadJobStatus.Ready.ToString(), result.Status);
        Assert.Single(result.Formats);
    }

    [Fact]
    public async Task StartDownloadAsync_QueuesReadyJob()
    {
        await using var dbContext = CreateDbContext();
        var queue = new FakeDownloadQueue();
        var service = CreateService(dbContext, new FakeUrlValidator(alwaysValid: true), new FakeProviderRegistry(hasProvider: true), queue);

        var analysis = await service.CreateAnalysisJobAsync(UserId, "https://example.com/video.mp4");
        var status = await service.StartDownloadAsync(analysis.JobId, UserId, "original");

        Assert.NotNull(status);
        Assert.Equal(DownloadJobStatus.Queued.ToString(), status!.Status);
        Assert.Contains(analysis.JobId, queue.Enqueued);
    }

    [Fact]
    public async Task StartDownloadAsync_FailsJob_WhenQueueIsFull()
    {
        await using var dbContext = CreateDbContext();
        var queue = new FakeDownloadQueue(alwaysAcceptsWork: false);
        var service = CreateService(dbContext, new FakeUrlValidator(alwaysValid: true), new FakeProviderRegistry(hasProvider: true), queue);

        var analysis = await service.CreateAnalysisJobAsync(UserId, "https://example.com/video.mp4");
        var status = await service.StartDownloadAsync(analysis.JobId, UserId, "original");

        Assert.NotNull(status);
        Assert.Equal(DownloadJobStatus.Failed.ToString(), status!.Status);
        Assert.Contains("queue", status.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancelJobAsync_CancelsActiveJob_ButNotCompletedJob()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, new FakeUrlValidator(alwaysValid: true), new FakeProviderRegistry(hasProvider: true), new FakeDownloadQueue());

        var analysis = await service.CreateAnalysisJobAsync(UserId, "https://example.com/video.mp4");

        Assert.True(await service.CancelJobAsync(analysis.JobId, UserId));

        var job = await dbContext.DownloadJobs.SingleAsync(j => j.Id == analysis.JobId);
        job.Status = DownloadJobStatus.Completed;
        await dbContext.SaveChangesAsync();

        Assert.False(await service.CancelJobAsync(analysis.JobId, UserId));
    }

    [Fact]
    public async Task GetCompletedFileAsync_ReturnsNull_UnlessJobCompletedWithAvailableFile()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, new FakeUrlValidator(alwaysValid: true), new FakeProviderRegistry(hasProvider: true), new FakeDownloadQueue());

        var analysis = await service.CreateAnalysisJobAsync(UserId, "https://example.com/video.mp4");

        Assert.Null(await service.GetCompletedFileAsync(analysis.JobId, UserId));

        var job = await dbContext.DownloadJobs.SingleAsync(j => j.Id == analysis.JobId);
        job.Status = DownloadJobStatus.Completed;
        dbContext.MediaFiles.Add(new MediaFile
        {
            DownloadJobId = job.Id,
            FileName = "video.mp4",
            StoragePath = "abc123.mp4",
            ContentType = "video/mp4",
            IsAvailable = true
        });
        await dbContext.SaveChangesAsync();

        var fileInfo = await service.GetCompletedFileAsync(analysis.JobId, UserId);
        Assert.NotNull(fileInfo);
        Assert.Equal("video.mp4", fileInfo!.FileName);
    }

    [Fact]
    public async Task GetJobStatusAsync_ReturnsNull_ForAnotherUsersJob()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, new FakeUrlValidator(alwaysValid: true), new FakeProviderRegistry(hasProvider: true), new FakeDownloadQueue());

        var analysis = await service.CreateAnalysisJobAsync(UserId, "https://example.com/video.mp4");

        var status = await service.GetJobStatusAsync(analysis.JobId, "someone-else");

        Assert.Null(status);
    }

    private static MediaGrabDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MediaGrabDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MediaGrabDbContext(options);
    }

    private static DownloadJobService CreateService(
        MediaGrabDbContext dbContext,
        IUrlValidator validator,
        IProviderRegistry registry,
        IDownloadQueue queue) =>
        new(dbContext, validator, registry, queue, NullLogger<DownloadJobService>.Instance);

    private class FakeUrlValidator : IUrlValidator
    {
        private readonly bool _alwaysValid;

        public FakeUrlValidator(bool alwaysValid) => _alwaysValid = alwaysValid;

        public UrlValidationResult Validate(string? rawUrl)
        {
            if (!_alwaysValid || string.IsNullOrWhiteSpace(rawUrl) || !Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
            {
                return UrlValidationResult.Failure("Invalid URL.");
            }

            return UrlValidationResult.Success(uri);
        }
    }

    private class FakeProviderRegistry : IProviderRegistry
    {
        private readonly IMediaProvider? _provider;

        public FakeProviderRegistry(bool hasProvider) => _provider = hasProvider ? new FakeMediaProvider() : null;

        public Task<IMediaProvider?> FindProviderAsync(Uri uri, CancellationToken cancellationToken = default) =>
            Task.FromResult(_provider);

        public IReadOnlyList<IMediaProvider> GetAllProviders() => _provider is null ? Array.Empty<IMediaProvider>() : new[] { _provider };
    }

    private class FakeMediaProvider : IMediaProvider
    {
        public string ProviderKey => "fake";
        public string DisplayName => "Fake Provider";

        public Task<bool> CanHandleAsync(Uri uri, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<ProviderResult<MediaMetadata>> AnalyzeAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            var metadata = new MediaMetadata
            {
                Title = "Fake Video",
                MediaType = MediaFileType.Video,
                EstimatedSizeBytes = 1024,
                Formats = new[] { new MediaFormat { FormatId = "original", Label = "Original", Extension = "mp4", EstimatedSizeBytes = 1024 } }
            };

            return Task.FromResult(ProviderResult<MediaMetadata>.Ok(metadata));
        }

        public Task<DownloadResult> DownloadAsync(Uri uri, MediaFormat format, Stream destination, IProgress<int>? progressPercent, CancellationToken cancellationToken = default) =>
            Task.FromResult(DownloadResult.Ok("fake.mp4", 1024, "video/mp4"));
    }

    private class FakeDownloadQueue : IDownloadQueue
    {
        private readonly bool _alwaysAcceptsWork;
        public List<Guid> Enqueued { get; } = new();

        public FakeDownloadQueue(bool alwaysAcceptsWork = true) => _alwaysAcceptsWork = alwaysAcceptsWork;

        public bool TryEnqueue(Guid jobId)
        {
            if (!_alwaysAcceptsWork)
            {
                return false;
            }

            Enqueued.Add(jobId);
            return true;
        }

        public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken) => AsyncEnumerable();

        private async IAsyncEnumerable<Guid> AsyncEnumerable()
        {
            foreach (var id in Enqueued)
            {
                yield return id;
            }

            await Task.CompletedTask;
        }

        public int ApproximateCount => Enqueued.Count;
    }
}
