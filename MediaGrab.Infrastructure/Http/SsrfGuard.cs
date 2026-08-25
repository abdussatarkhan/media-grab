using System.Net;
using System.Net.Sockets;

namespace MediaGrab.Infrastructure.Http;

/// <summary>
/// Centralizes the "is this IP safe to connect to" check so it can be
/// applied twice: once against the URL's declared host at validation
/// time (UrlValidator), and again against the IP actually resolved at
/// connection time (SsrfSafeConnectCallback). The second check is what
/// closes the DNS-rebinding gap - a hostname that resolved to a public
/// IP during validation but a private one moments later at connect time.
/// </summary>
public static class SsrfGuard
{
    public static bool IsPrivateOrReservedIp(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();

            if (bytes[0] == 10) return true; // 10.0.0.0/8
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true; // 172.16.0.0/12
            if (bytes[0] == 192 && bytes[1] == 168) return true; // 192.168.0.0/16
            if (bytes[0] == 169 && bytes[1] == 254) return true; // link-local, incl. cloud metadata 169.254.169.254
            if (bytes[0] == 0) return true; // 0.0.0.0/8
            if (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) return true; // 100.64.0.0/10 (CGNAT)
            if (bytes[0] >= 224) return true; // multicast/reserved
        }
        else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast) return true;
            if (ip.Equals(IPAddress.IPv6Loopback)) return true;

            var bytes = ip.GetAddressBytes();
            if ((bytes[0] & 0xfe) == 0xfc) return true; // fc00::/7 unique local
        }

        return false;
    }
}
