namespace OrderAggregationService.Services;

/// <summary>
/// Keeps the most recent hand-overs in memory, oldest evicted first.
/// </summary>
/// <remarks>
/// Bounded on purpose: this is a recent-activity view for an operator, so it must not
/// grow with uptime. Guarded by a single lock because the writer is the dispatch cycle
/// and the readers are dashboard polls, none of which are hot paths.
/// </remarks>
public sealed class InMemoryDispatchHistory : IDispatchHistory
{
    /// <summary>
    /// How many hand-overs are remembered. At the 20-second cadence this is the last
    /// several minutes of activity, which is what a dashboard glance needs.
    /// </summary>
    public const int Capacity = 20;

    private readonly Lock _gate = new();
    private readonly Queue<DispatchRecord> _records = new(Capacity);

    /// <inheritdoc />
    public void Record(DispatchRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        lock (_gate)
        {
            if (_records.Count == Capacity)
            {
                _records.Dequeue();
            }

            _records.Enqueue(record);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<DispatchRecord> GetRecent()
    {
        lock (_gate)
        {
            var newestFirst = _records.ToArray();

            Array.Reverse(newestFirst);

            return newestFirst;
        }
    }
}
