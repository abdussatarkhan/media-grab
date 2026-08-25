using MediaGrab.Application.Options;
using MediaGrab.Infrastructure.BackgroundServices;
using Microsoft.Extensions.Options;
using Xunit;

namespace MediaGrab.Tests.BackgroundServices;

public class DownloadQueueTests
{
    [Fact]
    public void TryEnqueue_SucceedsUpToConfiguredCapacity_ThenFails()
    {
        var queue = new DownloadQueue(Options.Create(new MediaGrabOptions { MaxQueuedJobs = 3 }));

        Assert.True(queue.TryEnqueue(Guid.NewGuid()));
        Assert.True(queue.TryEnqueue(Guid.NewGuid()));
        Assert.True(queue.TryEnqueue(Guid.NewGuid()));

        // Queue is now full - a burst of submissions must fail fast
        // rather than blocking or growing unbounded.
        Assert.False(queue.TryEnqueue(Guid.NewGuid()));
    }

    [Fact]
    public async Task DequeueAllAsync_YieldsEnqueuedItemsInOrder()
    {
        var queue = new DownloadQueue(Options.Create(new MediaGrabOptions { MaxQueuedJobs = 10 }));
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        queue.TryEnqueue(first);
        queue.TryEnqueue(second);

        using var cts = new CancellationTokenSource();
        var results = new List<Guid>();

        await foreach (var id in queue.DequeueAllAsync(cts.Token))
        {
            results.Add(id);
            if (results.Count == 2)
            {
                break;
            }
        }

        Assert.Equal(new[] { first, second }, results);
    }
}
