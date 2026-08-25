using MediaGrab.Application.Interfaces;
using MediaGrab.Application.Models;
using MediaGrab.Application.Options;
using MediaGrab.Domain.Enums;
using MediaGrab.Infrastructure.Http;
using MediaGrab.Infrastructure.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MediaGrab.Infrastructure.Providers;

/// <summary>
/// Reference provider implementation: handles a direct, publicly
/// accessible file URL (e.g. a link ending in a known file extension)
/// with no authentication or DRM involved.
///
/// AnalyzeAsync performs an authorized HTTP HEAD (falling back to a
/// ranged GET for servers that don't support HEAD well) to confirm the
/// resource exists and read its size/content-type - it never downloads
/// the whole file just to analyze it. DownloadAsync streams the body
/// directly to the destination stream with no in-memory buffering of
/// the full file, and enforces the configured max file size mid-stream
/// so an oversized or lying Content-Length header can't be used to
/// exhaust disk space.
/// </summary>
public class DirectFileProvider : IMediaProvider
{
    private static readonly string[] KnownExtensions =
    {
        ".mp4", ".mp3", ".wav", ".pdf", ".zip", ".jpg", ".jpeg", ".png", ".gif", ".mov", ".webm"
    };

    private readonly ISsrfSafeHttpClientFactory _httpClientFactory;
    private readonly MediaGrabOptions _options;
    private readonly ILogger<DirectFileProvider> _logger;

    public DirectFileProvider(
        ISsrfSafeHttpClientFactory httpClientFactory,
        IOptions<MediaGrabOptions> options,
        ILogger<DirectFileProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderKey => "direct-file";

    public string DisplayName => "Direct File Link";

    public Task<bool> CanHandleAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        var hasKnownExtension = KnownExtensions.Any(ext =>
            uri.AbsolutePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(hasKnownExtension);
    }

    public async Task<ProviderResult<MediaMetadata>> AnalyzeAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, uri);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            // Some direct-file hosts reject HEAD; fall back to a GET but
            // only read headers, then dispose without reading the body.
            using var fallback = response.IsSuccessStatusCode
                ? null
                : await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, uri), HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            var effective = fallback ?? response;

            if (!effective.IsSuccessStatusCode)
            {
                return ProviderResult<MediaMetadata>.Fail(
                    $"The remote server returned {(int)effective.StatusCode} {effective.ReasonPhrase} for this URL.");
            }

            var contentLength = effective.Content.Headers.ContentLength;
            if (contentLength.HasValue && contentLength.Value > _options.MaxFileSizeBytes)
            {
                return ProviderResult<MediaMetadata>.Fail(
                    $"This file ({FormatBytes(contentLength.Value)}) exceeds the maximum allowed size ({FormatBytes(_options.MaxFileSizeBytes)}).");
            }

            var rawFileName = uri.Segments.LastOrDefault()?.TrimEnd('/') ?? "download";
            var fileName = FileNameSanitizer.Sanitize(Uri.UnescapeDataString(rawFileName));
            var extension = Path.GetExtension(fileName);
            var mimeType = effective.Content.Headers.ContentType?.MediaType;

            var format = new MediaFormat
            {
                FormatId = "original",
                Label = "Original file",
                Extension = string.IsNullOrEmpty(extension) ? string.Empty : extension.TrimStart('.'),
                MimeType = mimeType,
                EstimatedSizeBytes = contentLength
            };

            var metadata = new MediaMetadata
            {
                Title = fileName,
                ThumbnailUrl = null, // direct files have no legitimate thumbnail source
                Duration = null,
                MediaType = InferMediaType(extension, mimeType),
                EstimatedSizeBytes = contentLength,
                Formats = new[] { format }
            };

            return ProviderResult<MediaMetadata>.Ok(metadata);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Analysis failed for a direct-file URL.");
            return ProviderResult<MediaMetadata>.Fail("Could not reach or analyze the provided URL.");
        }
    }

    public async Task<DownloadResult> DownloadAsync(
        Uri uri,
        MediaFormat format,
        Stream destination,
        IProgress<int>? progressPercent,
        CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return DownloadResult.Fail($"The remote server returned {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            var declaredLength = response.Content.Headers.ContentLength;
            if (declaredLength.HasValue && declaredLength.Value > _options.MaxFileSizeBytes)
            {
                return DownloadResult.Fail("The file exceeds the maximum allowed size.");
            }

            var rawFileName = uri.Segments.LastOrDefault()?.TrimEnd('/') ?? "download";
            var fileName = FileNameSanitizer.Sanitize(Uri.UnescapeDataString(rawFileName));
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);

            var buffer = new byte[81920];
            long totalRead = 0;
            int lastReportedPercent = -1;
            int read;

            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                totalRead += read;

                // Enforce the size cap mid-stream too - a server can omit
                // or lie about Content-Length, so we must not rely on the
                // header alone to bound how much we write to disk.
                if (totalRead > _options.MaxFileSizeBytes)
                {
                    return DownloadResult.Fail("The file exceeds the maximum allowed size and the download was aborted.");
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);

                if (declaredLength.HasValue && declaredLength.Value > 0 && progressPercent is not null)
                {
                    var percent = (int)Math.Min(99, totalRead * 100 / declaredLength.Value);
                    if (percent != lastReportedPercent)
                    {
                        progressPercent.Report(percent);
                        lastReportedPercent = percent;
                    }
                }
            }

            await destination.FlushAsync(cancellationToken);
            progressPercent?.Report(100);

            return DownloadResult.Ok(fileName, totalRead, contentType);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Download failed for a direct-file URL.");
            return DownloadResult.Fail("The download failed. The remote server may be unreachable or the file may no longer exist.");
        }
    }

    private static MediaFileType InferMediaType(string extension, string? mimeType)
    {
        var ext = extension.TrimStart('.').ToLowerInvariant();

        if (mimeType is not null)
        {
            if (mimeType.StartsWith("video/")) return MediaFileType.Video;
            if (mimeType.StartsWith("audio/")) return MediaFileType.Audio;
            if (mimeType.StartsWith("image/")) return MediaFileType.Image;
            if (mimeType is "application/pdf") return MediaFileType.Document;
        }

        return ext switch
        {
            "mp4" or "mov" or "webm" => MediaFileType.Video,
            "mp3" or "wav" => MediaFileType.Audio,
            "jpg" or "jpeg" or "png" or "gif" => MediaFileType.Image,
            "pdf" => MediaFileType.Document,
            _ => MediaFileType.Unknown
        };
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double size = bytes;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.#} {units[unitIndex]}";
    }
}
