namespace OrderAggregationService.Models;

/// <summary>
/// Aggregated quantity accumulated for a single product.
/// </summary>
/// <param name="ProductId">Identifier of the aggregated product.</param>
/// <param name="TotalQuantity">Sum of all accepted quantities for the product.</param>
/// <param name="OrderCount">Number of accepted orders that contributed to the total.</param>
/// <param name="FirstSeenUtc">Timestamp of the first accepted order for the product.</param>
/// <param name="LastUpdatedUtc">Timestamp of the most recently accepted order for the product.</param>
public sealed record AggregatedOrderItem(
    string ProductId,
    long TotalQuantity,
    int OrderCount,
    DateTimeOffset FirstSeenUtc,
    DateTimeOffset LastUpdatedUtc);
