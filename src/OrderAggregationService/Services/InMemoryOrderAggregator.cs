namespace OrderAggregationService.Services;

/// <summary>
/// Keeps the aggregate in process memory only.
/// </summary>
/// <remarks>
/// Inherits the whole accumulation algorithm and adds nothing: no restore, no persist.
/// That is the point of the base class - the default store carries no persistence code
/// at all, and the fastest option stays the simplest one.
/// </remarks>
public sealed class InMemoryOrderAggregator : OrderAggregatorBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryOrderAggregator"/> class.
    /// </summary>
    /// <param name="timeProvider">Clock used to stamp accepted orders.</param>
    /// <param name="logger">Logger for accepted requests.</param>
    public InMemoryOrderAggregator(TimeProvider timeProvider, ILogger<InMemoryOrderAggregator> logger)
        : base(timeProvider, logger)
    {
    }
}
