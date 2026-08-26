namespace OrderAggregationService.Tests;

/// <summary>
/// Records every hand-over and lets a test wait for one without sleeping.
/// </summary>
/// <remarks>
/// The wait is a blocking semaphore rather than an awaited task on purpose. The
/// background service resumes on the thread pool, while an awaited continuation inside
/// a test is posted back to xUnit's concurrency-limited synchronization context, whose
/// slots the test itself is holding. Blocking here waits on the pool instead and cannot
/// deadlock against that context.
/// </remarks>
internal sealed class RecordingOrderDispatcher : IOrderDispatcher, IDisposable
{
    private readonly Lock _gate = new();
    private readonly List<IReadOnlyList<AggregatedOrderItem>> _batches = [];
    private readonly SemaphoreSlim _dispatched = new(0);

    /// <summary>
    /// Gets a snapshot of everything dispatched so far.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<AggregatedOrderItem>> Batches
    {
        get
        {
            lock (_gate)
            {
                return [.. _batches];
            }
        }
    }

    /// <summary>
    /// Blocks until one more hand-over has happened.
    /// </summary>
    /// <param name="timeout">How long to wait before declaring the cycle broken.</param>
    public void WaitForDispatch(TimeSpan timeout)
    {
        if (!_dispatched.Wait(timeout))
        {
            throw new TimeoutException(
                $"The dispatch cycle produced no hand-over within {timeout}.");
        }
    }

    public Task DispatchAsync(
        IReadOnlyList<AggregatedOrderItem> aggregates,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _batches.Add(aggregates);
        }

        _dispatched.Release();

        return Task.CompletedTask;
    }

    public void Dispose() => _dispatched.Dispose();
}
