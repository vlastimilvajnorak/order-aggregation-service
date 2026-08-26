namespace OrderAggregationService.Models;

/// <summary>
/// An order that has already passed validation and is safe to aggregate.
/// </summary>
public sealed record Order
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Order"/> class.
    /// </summary>
    /// <param name="productId">Identifier of the ordered product.</param>
    /// <param name="quantity">Number of ordered units.</param>
    public Order(string productId, int quantity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        ProductId = productId;
        Quantity = quantity;
    }

    /// <summary>
    /// Gets the identifier of the ordered product.
    /// </summary>
    public string ProductId { get; }

    /// <summary>
    /// Gets the number of ordered units.
    /// </summary>
    public int Quantity { get; }
}
