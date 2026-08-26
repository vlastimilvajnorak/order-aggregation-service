namespace OrderAggregationService.Models;

/// <summary>
/// The document handed to the downstream system on every dispatch cycle.
/// </summary>
/// <remarks>
/// Declared as its own contract rather than reusing an internal model, so the shape
/// written to the console today is the shape a real integration would send tomorrow.
/// </remarks>
/// <param name="DispatchedAtUtc">When the hand-over was produced.</param>
/// <param name="ProductCount">Number of distinct products in the hand-over.</param>
/// <param name="TotalQuantity">Sum of the quantities across every product.</param>
/// <param name="Items">Accumulated quantity per product, ordered by product identifier.</param>
public sealed record OrderDispatchPayload(
    DateTimeOffset DispatchedAtUtc,
    int ProductCount,
    long TotalQuantity,
    IReadOnlyList<OrderDispatchItem> Items);

/// <summary>
/// One product in a dispatch payload.
/// </summary>
/// <param name="ProductId">Identifier of the product.</param>
/// <param name="Quantity">Accumulated quantity for the product.</param>
public sealed record OrderDispatchItem(string ProductId, long Quantity);
