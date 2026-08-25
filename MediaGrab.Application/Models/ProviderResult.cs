namespace MediaGrab.Application.Models;

/// <summary>
/// Generic outcome wrapper for provider operations (analysis, download).
/// Keeps success/failure explicit instead of providers throwing for
/// expected conditions like "URL not found" or "resource too large".
/// </summary>
public class ProviderResult<T>
{
    public bool Success { get; init; }

    public string? ErrorMessage { get; init; }

    public T? Data { get; init; }

    public static ProviderResult<T> Ok(T data) => new() { Success = true, Data = data };

    public static ProviderResult<T> Fail(string message) => new() { Success = false, ErrorMessage = message };
}
