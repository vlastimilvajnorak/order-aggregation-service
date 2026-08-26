namespace OrderAggregationService.Models;

/// <summary>
/// A point-in-time view of everything the aggregator currently holds.
/// </summary>
/// <param name="GeneratedAtUtc">Timestamp at which the snapshot was taken.</param>
/// <param name="ProductCount">Number of distinct products currently held.</param>
/// <param name="TotalQuantity">Sum of the quantities of all products currently held.</param>
/// <param name="AcceptedRequestCount">Number of requests accepted since startup, each carrying one or more orders.</param>
/// <param name="AcceptedOrderCount">Number of orders accepted since startup.</param>
/// <param name="Items">Aggregated products, ordered by product identifier.</param>
public sealed record AggregationSnapshot(
    DateTimeOffset GeneratedAtUtc,
    int ProductCount,
    long TotalQuantity,
    long AcceptedRequestCount,
    long AcceptedOrderCount,
    IReadOnlyList<AggregatedOrderItem> Items);
