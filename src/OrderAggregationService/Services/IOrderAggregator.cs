namespace OrderAggregationService.Services;

/// <summary>
/// Accumulates ordered quantities per product.
/// </summary>
/// <remarks>
/// This is the only aggregation contract the HTTP layer depends on. Replacing the
/// in-memory implementation with a relational or distributed store therefore does not
/// require any change to the endpoints or to the Blazor components. All members are
/// asynchronous so that an out-of-process implementation fits without a signature change,
/// and every implementation must be safe for concurrent use.
/// </remarks>
public interface IOrderAggregator
{
    /// <summary>
    /// Adds a request of validated orders to the aggregate.
    /// </summary>
    /// <param name="orders">Validated orders to accumulate.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A receipt describing what was accepted.</returns>
    ValueTask<OrderBatchReceipt> AggregateAsync(
        IReadOnlyList<Order> orders,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a consistent point-in-time view of the current aggregate.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The current snapshot.</returns>
    ValueTask<AggregationSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically removes and returns everything currently held.
    /// </summary>
    /// <remarks>
    /// Used by the periodic dispatch pipeline to hand the accumulated quantities over to a
    /// downstream system exactly once.
    /// </remarks>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The aggregated products that were removed, ordered by product identifier.</returns>
    ValueTask<IReadOnlyList<AggregatedOrderItem>> DrainAsync(CancellationToken cancellationToken = default);
}
