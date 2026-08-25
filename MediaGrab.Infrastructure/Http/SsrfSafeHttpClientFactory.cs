using System.Net;
using System.Net.Sockets;

namespace MediaGrab.Infrastructure.Http;

/// <summary>
/// Builds HttpClient instances whose underlying SocketsHttpHandler
/// resolves and validates the destination IP itself at connect time,
/// via ConnectCallback. This is the piece UrlValidator's own comments
/// call out as impossible to do "one layer up": a hostname can validate
/// safely and then, by the time the TCP handshake actually happens,
/// resolve to a private IP (DNS rebinding). Pinning + re-checking here
/// closes that gap.
/// </summary>
public class SsrfSafeHttpClientFactory : ISsrfSafeHttpClientFactory
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan OverallTimeout = TimeSpan.FromSeconds(30);

    public HttpClient CreateClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false, // callers must validate + follow redirects manually
            ConnectTimeout = ConnectTimeout,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectCallback = async (context, cancellationToken) =>
            {
                var host = context.DnsEndPoint.Host;
                var port = context.DnsEndPoint.Port;

                IPAddress[] addresses;
                if (IPAddress.TryParse(host, out var literal))
                {
                    addresses = new[] { literal };
                }
                else
                {
                    addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
                }

                if (addresses.Length == 0)
                {
                    throw new InvalidOperationException($"Could not resolve host '{host}'.");
                }

                // Reject the connection outright if ANY resolved address is
                // private/reserved - a multi-homed rebinding attempt could
                // otherwise pick whichever address looks safe first.
                if (addresses.Any(SsrfGuard.IsPrivateOrReservedIp))
                {
                    throw new InvalidOperationException(
                        $"Refusing to connect to '{host}': resolves to a private or reserved IP address.");
                }

                Exception? lastError = null;
                foreach (var address in addresses)
                {
                    var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true
                    };

                    try
                    {
                        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        connectCts.CancelAfter(ConnectTimeout);

                        await socket.ConnectAsync(new IPEndPoint(address, port), connectCts.Token);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        socket.Dispose();
                    }
                }

                throw lastError ?? new InvalidOperationException($"Unable to connect to host '{host}'.");
            }
        };

        var client = new HttpClient(handler)
        {
            Timeout = OverallTimeout
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("MediaGrab/1.0 (+authorized-access-only)");

        return client;
    }
}
