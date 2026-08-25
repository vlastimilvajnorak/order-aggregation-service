namespace OrderAggregationService.Services;

/// <summary>
/// Configuration of the order aggregation pipeline, bound from the
/// <c>OrderAggregation</c> configuration section.
/// </summary>
public sealed class OrderAggregationOptions
{
    /// <summary>
    /// Name of the configuration section these options are bound from.
    /// </summary>
    public const string SectionName = "OrderAggregation";

    /// <summary>
    /// Gets or sets a value indicating whether the periodic dispatch background service
    /// drains the aggregate and hands it over to <see cref="IOrderDispatcher"/>.
    /// </summary>
    /// <remarks>
    /// Disabled by default. The dispatch pipeline is a prepared extension point rather than
    /// a finished integration, and leaving it on would silently discard aggregates that no
    /// downstream system consumes yet.
    /// </remarks>
    public bool DispatchEnabled { get; set; }

    /// <summary>
    /// Gets or sets the interval between two dispatch cycles. Must be greater than zero.
    /// </summary>
    public TimeSpan DispatchInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the maximum number of order lines accepted in a single request.
    /// Must be greater than zero.
    /// </summary>
    public int MaxLinesPerRequest { get; set; } = 1000;
}
