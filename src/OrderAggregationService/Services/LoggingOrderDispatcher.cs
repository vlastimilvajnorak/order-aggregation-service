using OrderAggregationService.Models;

namespace OrderAggregationService.Services;

/// <summary>
/// Default <see cref="IOrderDispatcher"/> that only records what would be sent.
/// </summary>
/// <remarks>
/// Deliberately inert: it keeps the pipeline end-to-end runnable and observable without
/// pretending that a downstream integration exists.
/// </remarks>
public sealed class LoggingOrderDispatcher : IOrderDispatcher
{
    private readonly ILogger<LoggingOrderDispatcher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingOrderDispatcher"/> class.
    /// </summary>
    /// <param name="logger">Logger used to record dispatched aggregates.</param>
    public LoggingOrderDispatcher(ILogger<LoggingOrderDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    /// <inheritdoc />
    public Task DispatchAsync(
        IReadOnlyList<AggregatedOrderItem> aggregates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregates);
        cancellationToken.ThrowIfCancellationRequested();

        long totalQuantity = 0;

        foreach (var aggregate in aggregates)
        {
            totalQuantity += aggregate.TotalQuantity;
        }

        Log.AggregatesDispatched(_logger, aggregates.Count, totalQuantity);

        return Task.CompletedTask;
    }
}
