using System.Security.Claims;
using MediaGrab.Application.DTOs;
using MediaGrab.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MediaGrab.Web.Controllers.Api;

/// <summary>
/// Phase 2 API surface for the full job lifecycle: analyze, start
/// download, poll status, cancel, and stream the completed file.
/// Business logic lives entirely in IDownloadJobService/IFileStorageService
/// - this controller only handles HTTP concerns (routing, status codes,
/// auth, streaming) and stays thin.
/// </summary>
[ApiController]
[Route("api/media")]
[Authorize]
public class MediaController : ControllerBase
{
    private readonly IDownloadJobService _jobService;
    private readonly IFileStorageService _storage;
    private readonly ILogger<MediaController> _logger;

    public MediaController(IDownloadJobService jobService, IFileStorageService storage, ILogger<MediaController> logger)
    {
        _jobService = jobService;
        _storage = storage;
        _logger = logger;
    }

    /// <summary>Validate a submitted URL, detect its provider, and (if supported) analyze it.</summary>
    [HttpPost("analyze")]
    [EnableRateLimiting("analyze")]
    [ProducesResponseType(typeof(AnalyzeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Analyze([FromBody] AnalyzeRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ToErrorResponse());
        }

        var userId = GetUserId();

        var result = await _jobService.CreateAnalysisJobAsync(userId, request.Url, cancellationToken);

        if (!result.IsSupported && result.JobId == Guid.Empty)
        {
            // URL failed validation outright - never reached the database.
            return BadRequest(new ApiErrorResponse { Message = result.Message ?? "The URL could not be processed." });
        }

        return Ok(result);
    }

    /// <summary>Queue a previously analyzed job for background download processing.</summary>
    [HttpPost("download")]
    [EnableRateLimiting("download-start")]
    [ProducesResponseType(typeof(JobStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Download([FromBody] DownloadRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ToErrorResponse());
        }

        var userId = GetUserId();

        var status = await _jobService.StartDownloadAsync(request.JobId, userId, request.FormatId, cancellationToken);

        if (status is null)
        {
            return NotFound();
        }

        if (status.Status == "Failed" && string.Equals(status.ErrorMessage, "The download queue is currently full. Please try again shortly.", StringComparison.Ordinal))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ApiErrorResponse { Message = status.ErrorMessage });
        }

        return Ok(status);
    }

    /// <summary>Check the status/progress of a previously created job.</summary>
    [HttpGet("status/{id:guid}")]
    [EnableRateLimiting("api")]
    [ProducesResponseType(typeof(JobStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Status(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var status = await _jobService.GetJobStatusAsync(id, userId, cancellationToken);

        return status is null ? NotFound() : Ok(status);
    }

    /// <summary>Cancel a job that is not yet completed.</summary>
    [HttpPost("cancel/{id:guid}")]
    [EnableRateLimiting("api")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var cancelled = await _jobService.CancelJobAsync(id, userId, cancellationToken);

        return cancelled ? NoContent() : NotFound();
    }

    /// <summary>
    /// Stream a completed job's file to the caller. Verifies ownership,
    /// completion, and file existence; never loads the file fully into
    /// memory; uses a safe, sanitized filename; and deletes the temporary
    /// copy once the transfer finishes successfully (the file-cleanup
    /// background service is only the safety-net backup for this path).
    /// </summary>
    [HttpGet("file/{id:guid}")]
    [EnableRateLimiting("file-download")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> File(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var fileInfo = await _jobService.GetCompletedFileAsync(id, userId, cancellationToken);

        if (fileInfo is null)
        {
            return NotFound();
        }

        if (!await _storage.ExistsAsync(fileInfo.StorageKey, cancellationToken))
        {
            _logger.LogWarning("Job {JobId} is marked completed but its storage file is missing.", id);
            return NotFound();
        }

        // FileStreamResult streams directly from disk to the response
        // body - the file is never buffered fully in memory, and ASP.NET
        // Core handles Range requests (resumable downloads) automatically
        // when EnableRangeProcessing is set.
        var stream = await _storage.OpenReadAsync(fileInfo.StorageKey, cancellationToken);

        Response.OnCompleted(async () =>
        {
            // Only delete after the response has actually finished
            // streaming (successfully or via a completed/interrupted
            // connection close) - never while bytes are still being sent.
            try
            {
                await _storage.DeleteAsync(fileInfo.StorageKey, CancellationToken.None);
                await _jobService.MarkFileConsumedAsync(id, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Post-download cleanup failed for job {JobId}.", id);
            }
        });

        return new FileStreamResult(stream, fileInfo.ContentType)
        {
            FileDownloadName = fileInfo.FileName,
            EnableRangeProcessing = true
        };
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private ApiErrorResponse ToErrorResponse()
    {
        var errors = ModelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

        return new ApiErrorResponse
        {
            Message = "One or more fields are invalid.",
            Errors = errors
        };
    }
}
