namespace OrderAggregationService.Models;

/// <summary>
/// What one completed hand-over carried.
/// </summary>
/// <remarks>
/// A hand-over empties the aggregate, so without this the orders would vanish from every
/// view the moment they were sent. Keeping a short history makes the service auditable
/// from the outside: it answers "what went out, and when".
/// </remarks>
/// <param name="DispatchedAtUtc">When the hand-over was made.</param>
/// <param name="ProductCount">Number of distinct products it carried.</param>
/// <param name="TotalQuantity">Sum of the quantities it carried.</param>
public sealed record DispatchRecord(
    DateTimeOffset DispatchedAtUtc,
    int ProductCount,
    long TotalQuantity);
