namespace OrderAggregationService.Services;

/// <summary>
/// Keeps the most recent hand-overs so they remain visible after the aggregate is
/// drained.
/// </summary>
/// <remarks>
/// Deliberately bounded and in-memory: this is an operator's recent-activity view, not
/// an audit trail. A durable record of every dispatched batch is listed in the README as
/// a production concern, and would live wherever the aggregate itself is persisted.
/// </remarks>
public interface IDispatchHistory
{
    /// <summary>
    /// Records a completed hand-over, evicting the oldest when the history is full.
    /// </summary>
    /// <param name="record">The hand-over to remember.</param>
    void Record(DispatchRecord record);

    /// <summary>
    /// Returns the recent hand-overs, newest first.
    /// </summary>
    /// <returns>The remembered hand-overs.</returns>
    IReadOnlyList<DispatchRecord> GetRecent();
}
