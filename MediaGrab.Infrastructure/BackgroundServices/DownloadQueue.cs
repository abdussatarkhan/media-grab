using System.Threading.Channels;
using MediaGrab.Application.Interfaces;
using MediaGrab.Application.Options;
using Microsoft.Extensions.Options;

namespace MediaGrab.Infrastructure.BackgroundServices;

/// <summary>
/// Bounded in-memory queue backing the background download worker. Bounded
/// capacity (MediaGrab:MaxQueuedJobs) means a burst of submissions fails
/// fast with a 503 instead of consuming unbounded memory - callers must
/// handle TryEnqueue returning false.
/// </summary>
public class DownloadQueue : IDownloadQueue
{
    private readonly Channel<Guid> _channel;

    public DownloadQueue(IOptions<MediaGrabOptions> options)
    {
        var capacity = Math.Max(1, options.Value.MaxQueuedJobs);
        _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(capacity)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public bool TryEnqueue(Guid jobId) => _channel.Writer.TryWrite(jobId);

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);

    public int ApproximateCount => _channel.Reader.Count;
}
