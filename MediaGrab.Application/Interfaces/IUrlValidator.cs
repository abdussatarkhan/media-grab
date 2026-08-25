namespace MediaGrab.Application.Interfaces;

/// <summary>
/// Validates and normalizes user-supplied URLs before anything else in
/// the system ever touches them. Every entry point (API, Razor Pages)
/// must go through this - never trust a raw string from the browser.
/// </summary>
public interface IUrlValidator
{
    UrlValidationResult Validate(string? rawUrl);
}

public class UrlValidationResult
{
    public bool IsValid { get; init; }

    public Uri? NormalizedUri { get; init; }

    public string? ErrorMessage { get; init; }

    public static UrlValidationResult Success(Uri uri) =>
        new() { IsValid = true, NormalizedUri = uri };

    public static UrlValidationResult Failure(string message) =>
        new() { IsValid = false, ErrorMessage = message };
}
