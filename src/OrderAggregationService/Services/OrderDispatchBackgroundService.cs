using Microsoft.Extensions.Options;

namespace OrderAggregationService.Services;

/// <summary>
/// Periodically drains the aggregate and hands it over to <see cref="IOrderDispatcher"/>.
/// </summary>
/// <remarks>
/// The full production dispatch behaviour (retries, dead-lettering, back-pressure) is out of
/// scope for this scaffold. What is in place is the cycle itself, so a real
/// <see cref="IOrderDispatcher"/> can be plugged in without touching the schedule.
/// </remarks>
public sealed class OrderDispatchBackgroundService : BackgroundService
{
    private readonly IOrderAggregator _aggregator;
    private readonly IOrderDispatcher _dispatcher;
    private readonly OrderAggregationOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OrderDispatchBackgroundService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderDispatchBackgroundService"/> class.
    /// </summary>
    /// <param name="aggregator">Aggregator to drain.</param>
    /// <param name="dispatcher">Downstream dispatcher.</param>
    /// <param name="options">Dispatch configuration.</param>
    /// <param name="timeProvider">Clock driving the dispatch timer.</param>
    /// <param name="logger">Logger for dispatch cycles.</param>
    public OrderDispatchBackgroundService(
        IOrderAggregator aggregator,
        IOrderDispatcher dispatcher,
        IOptions<OrderAggregationOptions> options,
        TimeProvider timeProvider,
        ILogger<OrderDispatchBackgroundService> logger)
    {
        ArgumentNullException.ThrowIfNull(aggregator);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _aggregator = aggregator;
        _dispatcher = dispatcher;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.DispatchEnabled)
        {
            Log.DispatchDisabled(_logger);
            return;
        }

        Log.DispatchStarted(_logger, _options.DispatchInterval);

        using var timer = new PeriodicTimer(_options.DispatchInterval, _timeProvider);
        long cycleNumber = 0;

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            cycleNumber++;

            try
            {
                await RunCycleAsync(cycleNumber, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
#pragma warning disable CA1031 // A failing cycle must never terminate the dispatch loop.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                Log.DispatchCycleFailed(_logger, cycleNumber, exception);
            }
        }
    }

    private async Task RunCycleAsync(long cycleNumber, CancellationToken cancellationToken)
    {
        var pending = await _aggregator.DrainAsync(cancellationToken).ConfigureAwait(false);

        if (pending.Count == 0)
        {
            Log.DispatchCycleEmpty(_logger, cycleNumber);
            return;
        }

        await _dispatcher.DispatchAsync(pending, cancellationToken).ConfigureAwait(false);

        long totalQuantity = 0;

        foreach (var aggregate in pending)
        {
            totalQuantity += aggregate.TotalQuantity;
        }

        Log.DispatchCycleCompleted(_logger, cycleNumber, pending.Count, totalQuantity);
    }
}
