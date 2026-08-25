using System.Net;
using System.Net.Sockets;
using MediaGrab.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace MediaGrab.Infrastructure.Validators;

/// <summary>
/// Validates and normalizes user-submitted URLs before any other part of
/// the system touches them. This is a defense-in-depth building block:
/// it rejects malformed input, non-HTTP(S) schemes, and obvious
/// SSRF targets (localhost, private/link-local/loopback IP ranges).
///
/// Note: DNS-based SSRF (a public hostname that resolves to a private IP
/// at request time) cannot be fully closed here because Phase 1 never
/// makes an outbound request. Phase 2's HTTP client must re-resolve and
/// re-check the destination IP immediately before connecting.
/// </summary>
public class UrlValidator : IUrlValidator
{
    private const int MaxUrlLength = 2048;

    private static readonly HashSet<string> AllowedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        Uri.UriSchemeHttp,
        Uri.UriSchemeHttps
    };

    private static readonly string[] BlockedHostSuffixes =
    {
        "localhost",
        ".local",
        ".internal"
    };

    private readonly ILogger<UrlValidator> _logger;

    public UrlValidator(ILogger<UrlValidator> logger)
    {
        _logger = logger;
    }

    public UrlValidationResult Validate(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return UrlValidationResult.Failure("A URL is required.");
        }

        var trimmed = rawUrl.Trim();

        if (trimmed.Length > MaxUrlLength)
        {
            _logger.LogWarning("Rejected URL: exceeds max length of {MaxLength}.", MaxUrlLength);
            return UrlValidationResult.Failure($"URL must be {MaxUrlLength} characters or fewer.");
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return UrlValidationResult.Failure("The URL is not well-formed.");
        }

        if (!AllowedSchemes.Contains(uri.Scheme))
        {
            _logger.LogWarning("Rejected URL with disallowed scheme {Scheme}.", uri.Scheme);
            return UrlValidationResult.Failure("Only http:// and https:// URLs are supported.");
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            return UrlValidationResult.Failure("The URL is missing a host name.");
        }

        if (IsBlockedHost(uri.Host))
        {
            _logger.LogWarning("Rejected URL targeting a blocked/private host.");
            return UrlValidationResult.Failure("URLs pointing to local or internal network addresses are not allowed.");
        }

        // Normalize: strip default ports, lowercase scheme/host, drop fragment.
        var builder = new UriBuilder(uri) { Fragment = string.Empty };
        if ((builder.Scheme == Uri.UriSchemeHttp && builder.Port == 80) ||
            (builder.Scheme == Uri.UriSchemeHttps && builder.Port == 443))
        {
            builder.Port = -1;
        }

        return UrlValidationResult.Success(builder.Uri);
    }

    private static bool IsBlockedHost(string host)
    {
        var lowerHost = host.ToLowerInvariant();

        foreach (var suffix in BlockedHostSuffixes)
        {
            if (lowerHost == suffix.TrimStart('.') || lowerHost.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (IPAddress.TryParse(host, out var ip))
        {
            return IsPrivateOrReservedIp(ip);
        }

        return false;
    }

    private static bool IsPrivateOrReservedIp(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();

            // 10.0.0.0/8
            if (bytes[0] == 10) return true;

            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;

            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168) return true;

            // 169.254.0.0/16 (link-local, includes cloud metadata endpoint 169.254.169.254)
            if (bytes[0] == 169 && bytes[1] == 254) return true;

            // 0.0.0.0/8
            if (bytes[0] == 0) return true;
        }
        else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast) return true;
            if (ip.Equals(IPAddress.IPv6Loopback)) return true;
            // fc00::/7 unique local
            var bytes = ip.GetAddressBytes();
            if ((bytes[0] & 0xfe) == 0xfc) return true;
        }

        return false;
    }
}
