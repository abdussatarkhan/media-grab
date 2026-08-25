using System.Net;
using System.Text.Json;
using MediaGrab.Application.DTOs;

namespace MediaGrab.Web.Middleware;

/// <summary>
/// Global exception handler. Any unhandled exception is logged with full
/// detail server-side, but the client only ever receives a generic,
/// safe message plus a trace id they can quote in a support request -
/// never a stack trace, connection string, or file path.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var traceId = context.TraceIdentifier;

            _logger.LogError(ex, "Unhandled exception. TraceId={TraceId}", traceId);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var response = new ApiErrorResponse
            {
                Message = _environment.IsDevelopment()
                    ? ex.Message
                    : "An unexpected error occurred. Please try again later.",
                TraceId = traceId
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
