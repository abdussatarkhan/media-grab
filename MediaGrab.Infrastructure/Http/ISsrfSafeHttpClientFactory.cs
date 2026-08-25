namespace MediaGrab.Infrastructure.Http;

/// <summary>
/// Produces HttpClient instances hardened against SSRF: every outbound
/// connection resolves DNS itself, rejects private/loopback/link-local
/// targets at the moment of connecting (not just at URL-validation time),
/// disables automatic redirects (the caller must re-validate and follow
/// redirects manually), and enforces sane timeouts.
/// </summary>
public interface ISsrfSafeHttpClientFactory
{
    HttpClient CreateClient();
}
