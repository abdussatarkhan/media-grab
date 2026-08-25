namespace MediaGrab.Application.DTOs;

/// <summary>
/// A consistent, safe-to-expose error shape returned by every API
/// endpoint and by the global exception handler. Never carries stack
/// traces, connection strings, or internal paths.
/// </summary>
public class ApiErrorResponse
{
    public string Message { get; set; } = string.Empty;

    public string? TraceId { get; set; }

    public IReadOnlyDictionary<string, string[]>? Errors { get; set; }
}
