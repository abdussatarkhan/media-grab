namespace MediaGrab.Application.Interfaces;

/// <summary>
/// A bounded FIFO queue of job ids waiting for a background worker slot.
/// Bounded so a burst of submissions fails fast (503) instead of growing
/// memory usage without limit.
/// </summary>
public interface IDownloadQueue
{
    /// <summary>Attempt to enqueue a job id. Returns false if the queue is full.</summary>
    bool TryEnqueue(Guid jobId);

    IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken);

    int ApproximateCount { get; }
}
