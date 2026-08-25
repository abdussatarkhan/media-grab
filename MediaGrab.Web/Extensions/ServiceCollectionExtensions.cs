using System.Threading.RateLimiting;
using MediaGrab.Application.Interfaces;
using MediaGrab.Application.Options;
using MediaGrab.Infrastructure.BackgroundServices;
using MediaGrab.Infrastructure.Http;
using MediaGrab.Infrastructure.Providers;
using MediaGrab.Infrastructure.Services;
using MediaGrab.Infrastructure.Storage;
using MediaGrab.Infrastructure.Validators;
using Microsoft.AspNetCore.RateLimiting;

namespace MediaGrab.Web.Extensions;

/// <summary>
/// Wires up application/infrastructure services. Keeping this out of
/// Program.cs makes it obvious, in one place, everything the app depends
/// on - and it's where a new IMediaProvider gets registered.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMediaGrabServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MediaGrabOptions>(configuration.GetSection(MediaGrabOptions.SectionName));

        services.AddScoped<IUrlValidator, UrlValidator>();
        services.AddScoped<IProviderRegistry, ProviderRegistry>();
        services.AddScoped<IDownloadJobService, DownloadJobService>();

        services.AddSingleton<ISsrfSafeHttpClientFactory, SsrfSafeHttpClientFactory>();
        services.AddSingleton<IFileStorageService, LocalFileStorageService>();
        services.AddSingleton<IDownloadQueue, DownloadQueue>();

        // Register each authorized provider here. Adding a new provider
        // is a one-line addition and never requires touching the
        // registry, controllers, or Razor Pages.
        services.AddScoped<IMediaProvider, DirectFileProvider>();

        // Background processing: a single queue reader bounded to
        // MaxConcurrentJobs in-flight transfers, plus a periodic
        // cleanup sweep. Both are hosted services started with the app.
        services.AddHostedService<DownloadProcessingWorker>();
        services.AddHostedService<FileCleanupService>();

        return services;
    }

    /// <summary>
    /// ASP.NET Core rate limiting with named policies for each sensitive
    /// endpoint category. Limits themselves come from configuration
    /// (MediaGrab:RateLimits), never hard-coded.
    /// </summary>
    public static IServiceCollection AddMediaGrabRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var limits = configuration.GetSection(MediaGrabOptions.SectionName).Get<MediaGrabOptions>()?.RateLimits
                     ?? new RateLimitOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy("analyze", context => RateLimitPartition.GetFixedWindowLimiter(
                PartitionKey(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, limits.AnalyzePerMinute),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

            options.AddPolicy("download-start", context => RateLimitPartition.GetFixedWindowLimiter(
                PartitionKey(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, limits.DownloadStartPerMinute),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

            options.AddPolicy("file-download", context => RateLimitPartition.GetFixedWindowLimiter(
                PartitionKey(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, limits.FileDownloadPerMinute),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

            options.AddPolicy("api", context => RateLimitPartition.GetFixedWindowLimiter(
                PartitionKey(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, limits.GeneralApiPerMinute),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

            options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
                PartitionKey(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, limits.AuthAttemptsPer5Minutes),
                    Window = TimeSpan.FromMinutes(5),
                    QueueLimit = 0
                }));
        });

        return services;
    }

    private static string PartitionKey(HttpContext context)
    {
        // Prefer the authenticated user id when available (post-auth
        // endpoints), fall back to remote IP for anonymous endpoints
        // like login/register.
        var userId = context.User?.Identity?.IsAuthenticated == true
            ? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            : null;

        return userId ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
