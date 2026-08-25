using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OrderAggregationService.Services;

/// <summary>
/// Reports whether the aggregation pipeline is able to answer queries, and exposes the
/// current backlog as health check data.
/// </summary>
internal sealed class OrderAggregationHealthCheck : IHealthCheck
{
    /// <summary>
    /// Name under which this check is registered.
    /// </summary>
    public const string Name = "order-aggregation";

    private readonly IOrderAggregator _aggregator;

    public OrderAggregationHealthCheck(IOrderAggregator aggregator)
    {
        ArgumentNullException.ThrowIfNull(aggregator);

        _aggregator = aggregator;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _aggregator.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        var data = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["pendingProducts"] = snapshot.ProductCount,
            ["pendingQuantity"] = snapshot.TotalQuantity,
            ["acceptedBatches"] = snapshot.AcceptedBatchCount,
            ["acceptedLines"] = snapshot.AcceptedLineCount,
        };

        return HealthCheckResult.Healthy("The order aggregator is accepting orders.", data);
    }
}
