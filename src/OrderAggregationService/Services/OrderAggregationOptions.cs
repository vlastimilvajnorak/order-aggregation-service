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
    /// Minimum interval between two hand-overs to the downstream system.
    /// </summary>
    /// <remarks>
    /// The specification requires that aggregated orders are handed over no more often
    /// than once every 20 seconds, so this is a floor rather than a target. Nothing may
    /// dispatch earlier because a batch looks large or a queue looks full.
    /// </remarks>
    public static readonly TimeSpan DefaultDispatchInterval = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Gets or sets a value indicating whether the periodic dispatch background service
    /// drains the aggregate and hands it over to <see cref="IOrderDispatcher"/>.
    /// </summary>
    /// <remarks>
    /// Enabled by default: handing the accumulated totals on is part of the service, not
    /// an optional extra. Switch it off only to isolate the API in a test.
    /// </remarks>
    public bool DispatchEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum interval between two dispatch cycles. Must be greater
    /// than zero. Defaults to <see cref="DefaultDispatchInterval"/>.
    /// </summary>
    public TimeSpan DispatchInterval { get; set; } = DefaultDispatchInterval;

    /// <summary>
    /// Gets or sets the maximum number of orders accepted in a single request.
    /// Must be greater than zero.
    /// </summary>
    public int MaxOrdersPerRequest { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the maximum length of a product identifier. Must be greater than zero.
    /// </summary>
    /// <remarks>
    /// A bound is needed rather than merely tidy. Every distinct identifier creates an
    /// entry that lives until the next hand-over, so an unbounded identifier lets a caller
    /// grow the aggregate by the size of what it sends rather than by the orders it
    /// places. 64 characters covers any realistic product code.
    /// </remarks>
    public int MaxProductIdLength { get; set; } = 64;
}
