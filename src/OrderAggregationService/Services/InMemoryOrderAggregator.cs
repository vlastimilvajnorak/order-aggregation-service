using OrderAggregationService.Models;

namespace OrderAggregationService.Services;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IOrderAggregator"/>.
/// </summary>
/// <remarks>
/// State is guarded by a single <see cref="Lock"/>. The accumulated values are plain
/// counters, so the critical sections are extremely short and a single lock keeps
/// snapshots and drains atomic, which a per-entry lock could not guarantee.
/// This implementation is intentionally process-local: it is registered behind
/// <see cref="IOrderAggregator"/> so it can be swapped for a durable or distributed store.
/// </remarks>
public sealed class InMemoryOrderAggregator : IOrderAggregator
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, ProductAccumulator> _products = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<InMemoryOrderAggregator> _logger;

    private long _acceptedBatchCount;
    private long _acceptedLineCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryOrderAggregator"/> class.
    /// </summary>
    /// <param name="timeProvider">Clock used to stamp accepted order lines.</param>
    /// <param name="logger">Logger for accepted batches.</param>
    public InMemoryOrderAggregator(TimeProvider timeProvider, ILogger<InMemoryOrderAggregator> logger)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public ValueTask<OrderBatchReceipt> AggregateAsync(
        IReadOnlyList<OrderLine> lines,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lines);
        cancellationToken.ThrowIfCancellationRequested();

        var receivedAtUtc = _timeProvider.GetUtcNow();
        long acceptedQuantity = 0;

        lock (_gate)
        {
            foreach (var line in lines)
            {
                if (!_products.TryGetValue(line.ProductId, out var accumulator))
                {
                    accumulator = new ProductAccumulator(receivedAtUtc);
                    _products[line.ProductId] = accumulator;
                }

                accumulator.Add(line.Quantity, receivedAtUtc);
                acceptedQuantity += line.Quantity;
            }

            _acceptedBatchCount++;
            _acceptedLineCount += lines.Count;
        }

        var receipt = new OrderBatchReceipt(Guid.CreateVersion7(), lines.Count, acceptedQuantity, receivedAtUtc);
        Log.OrderBatchAccepted(_logger, receipt.BatchId, receipt.AcceptedLineCount, receipt.AcceptedQuantity);

        return ValueTask.FromResult(receipt);
    }

    /// <inheritdoc />
    public ValueTask<AggregationSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        AggregationSnapshot snapshot;

        lock (_gate)
        {
            var items = BuildOrderedItems();
            long totalQuantity = 0;

            foreach (var item in items)
            {
                totalQuantity += item.TotalQuantity;
            }

            snapshot = new AggregationSnapshot(
                _timeProvider.GetUtcNow(),
                items.Count,
                totalQuantity,
                _acceptedBatchCount,
                _acceptedLineCount,
                items);
        }

        return ValueTask.FromResult(snapshot);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<AggregatedOrderItem>> DrainAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<AggregatedOrderItem> drained;

        lock (_gate)
        {
            drained = BuildOrderedItems();
            _products.Clear();
        }

        return ValueTask.FromResult(drained);
    }

    /// <summary>
    /// Materializes the current products ordered by identifier. Must be called under <see cref="_gate"/>.
    /// </summary>
    /// <returns>The aggregated products.</returns>
    private List<AggregatedOrderItem> BuildOrderedItems()
    {
        var items = new List<AggregatedOrderItem>(_products.Count);

        foreach (var (productId, accumulator) in _products)
        {
            items.Add(accumulator.ToAggregate(productId));
        }

        items.Sort(static (left, right) => string.CompareOrdinal(left.ProductId, right.ProductId));

        return items;
    }

    /// <summary>
    /// Mutable counters for a single product. All members are accessed under the owner lock.
    /// </summary>
    private sealed class ProductAccumulator
    {
        private readonly DateTimeOffset _firstSeenUtc;
        private long _totalQuantity;
        private int _lineCount;
        private DateTimeOffset _lastUpdatedUtc;

        public ProductAccumulator(DateTimeOffset firstSeenUtc)
        {
            _firstSeenUtc = firstSeenUtc;
            _lastUpdatedUtc = firstSeenUtc;
        }

        public void Add(int quantity, DateTimeOffset timestampUtc)
        {
            _totalQuantity += quantity;
            _lineCount++;
            _lastUpdatedUtc = timestampUtc;
        }

        public AggregatedOrderItem ToAggregate(string productId) =>
            new(productId, _totalQuantity, _lineCount, _firstSeenUtc, _lastUpdatedUtc);
    }
}
