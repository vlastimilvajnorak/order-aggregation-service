using System.Text.Json;

namespace OrderAggregationService.Services;

/// <summary>
/// Default <see cref="IOrderDispatcher"/> that writes the aggregated orders to the
/// console as JSON.
/// </summary>
/// <remarks>
/// This stands in for the real downstream integration. It is deliberately the whole
/// hand-over: the payload written here is the document a real integration would send,
/// so replacing this class does not change what the downstream system receives.
/// </remarks>
public sealed class ConsoleOrderDispatcher : IOrderDispatcher
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = false };

    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ConsoleOrderDispatcher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleOrderDispatcher"/> class.
    /// </summary>
    /// <param name="timeProvider">Clock used to stamp the hand-over.</param>
    /// <param name="logger">Logger the payload is written through.</param>
    public ConsoleOrderDispatcher(TimeProvider timeProvider, ILogger<ConsoleOrderDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Builds the JSON document handed to the downstream system.
    /// </summary>
    /// <remarks>
    /// Exposed separately from <see cref="DispatchAsync"/> so the wire shape can be
    /// asserted directly, without capturing log output.
    /// </remarks>
    /// <param name="dispatchedAtUtc">Timestamp recorded in the payload.</param>
    /// <param name="aggregates">The drained aggregates.</param>
    /// <returns>The serialized payload.</returns>
    public static string CreatePayloadJson(
        DateTimeOffset dispatchedAtUtc,
        IReadOnlyList<AggregatedOrderItem> aggregates)
    {
        ArgumentNullException.ThrowIfNull(aggregates);

        var items = new List<OrderDispatchItem>(aggregates.Count);
        long totalQuantity = 0;

        foreach (var aggregate in aggregates)
        {
            items.Add(new OrderDispatchItem(aggregate.ProductId, aggregate.TotalQuantity));
            totalQuantity += aggregate.TotalQuantity;
        }

        var payload = new OrderDispatchPayload(dispatchedAtUtc, items.Count, totalQuantity, items);

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    /// <inheritdoc />
    public Task DispatchAsync(
        IReadOnlyList<AggregatedOrderItem> aggregates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregates);
        cancellationToken.ThrowIfCancellationRequested();

        var payloadJson = CreatePayloadJson(_timeProvider.GetUtcNow(), aggregates);

        // The specification says the hand-over is written to the console as JSON, so the
        // payload goes to stdout as its own line, with nothing prefixed - a consumer can
        // parse the line as-is. The logger writes whole lines too, so the two writers
        // never interleave mid-line. The log entry alongside it carries the summary for
        // operators without duplicating the payload.
        Console.Out.WriteLine(payloadJson);

        Log.AggregatesDispatched(_logger, aggregates.Count);

        return Task.CompletedTask;
    }
}
