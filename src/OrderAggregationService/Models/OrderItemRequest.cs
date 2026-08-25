namespace OrderAggregationService.Models;

/// <summary>
/// A single order line as submitted by an API client.
/// </summary>
/// <param name="ProductId">Identifier of the ordered product. Must not be empty.</param>
/// <param name="Quantity">Number of ordered units. Must be greater than zero.</param>
public sealed record OrderItemRequest(string? ProductId, int Quantity);
