namespace OrderAggregationService.Services;

/// <summary>
/// Hands aggregated quantities over to a downstream system.
/// </summary>
/// <remarks>
/// This is the seam for the eventual production integration, for example a supplier API,
/// a message broker, or a batch export. The periodic dispatch pipeline depends on this
/// contract only, so adding a real integration does not touch the aggregation code.
/// </remarks>
public interface IOrderDispatcher
{
    /// <summary>
    /// Dispatches a drained set of aggregated products.
    /// </summary>
    /// <param name="aggregates">The aggregated products to dispatch. Never empty.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes once the hand-over finished.</returns>
    Task DispatchAsync(IReadOnlyList<AggregatedOrderItem> aggregates, CancellationToken cancellationToken = default);
}
