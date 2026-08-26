namespace OrderAggregationService.Tests;

/// <summary>
/// The real history, plus a signal a test can wait on.
/// </summary>
/// <remarks>
/// The background service records a hand-over only after the dispatcher has accepted it,
/// so waiting on the dispatcher wakes a test up one step too early: the hand-over has
/// happened, but the history has not been written yet. Tests that assert on the history
/// have to wait on the history.
/// </remarks>
internal sealed class RecordingDispatchHistory : IDispatchHistory, IDisposable
{
    private readonly InMemoryDispatchHistory _history = new();
    private readonly SemaphoreSlim _recorded = new(0);

    /// <summary>
    /// Blocks until one more hand-over has been recorded.
    /// </summary>
    /// <param name="timeout">How long to wait before declaring the cycle broken.</param>
    public void WaitForRecord(TimeSpan timeout)
    {
        if (!_recorded.Wait(timeout))
        {
            throw new TimeoutException(
                $"No hand-over reached the history within {timeout}.");
        }
    }

    public void Record(DispatchRecord record)
    {
        _history.Record(record);

        _recorded.Release();
    }

    public IReadOnlyList<DispatchRecord> GetRecent() => _history.GetRecent();

    public void Dispose() => _recorded.Dispose();
}
