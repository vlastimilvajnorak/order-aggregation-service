namespace OrderAggregationService.Services;

/// <summary>
/// The accumulation semantics every store shares, with persistence left to the
/// derived class.
/// </summary>
/// <remarks>
/// A template method rather than composition, because the algorithm is fixed and only
/// three steps vary: what is loaded at startup, what is written when a request is
/// accepted, and what is written when the aggregate is handed over. Putting the summing,
/// the locking and the counters here means a new store cannot accidentally change how
/// quantities aggregate - it can only change where they are kept.
///
/// The write hooks carry deltas, not a snapshot of everything. That is deliberate: the
/// store this seam exists for is a database, where an accepted request is an upsert of a
/// few rows (<c>quantity = quantity + @delta</c>) and a hand-over is a delete. Handing the
/// whole aggregate to the store on every request would force a full rewrite and would not
/// survive hundreds of requests per second over hundreds of products.
///
/// State is guarded by a single <see cref="Lock"/>. The accumulated values are plain
/// counters, so the critical sections are extremely short and one lock keeps snapshots
/// and drains atomic, which a per-entry lock could not guarantee.
/// </remarks>
public abstract class OrderAggregatorBase : IOrderAggregator
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, ProductAccumulator> _products = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    private long _acceptedRequestCount;
    private long _acceptedOrderCount;
    private bool _restored;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderAggregatorBase"/> class.
    /// </summary>
    /// <param name="timeProvider">Clock used to stamp accepted orders.</param>
    /// <param name="logger">Logger for accepted requests.</param>
    protected OrderAggregatorBase(TimeProvider timeProvider, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public ValueTask<OrderBatchReceipt> AggregateAsync(
        IReadOnlyList<Order> orders,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orders);
        cancellationToken.ThrowIfCancellationRequested();

        EnsureRestored();

        var receivedAtUtc = _timeProvider.GetUtcNow();
        long acceptedQuantity = 0;

        lock (_gate)
        {
            foreach (var order in orders)
            {
                if (!_products.TryGetValue(order.ProductId, out var accumulator))
                {
                    accumulator = new ProductAccumulator(receivedAtUtc);
                    _products[order.ProductId] = accumulator;
                }

                accumulator.Add(order.Quantity, receivedAtUtc);
                acceptedQuantity += order.Quantity;
            }

            _acceptedRequestCount++;
            _acceptedOrderCount += orders.Count;
        }

        var receipt = new OrderBatchReceipt(
            Guid.CreateVersion7(), orders.Count, acceptedQuantity, receivedAtUtc);

        Log.OrderBatchAccepted(_logger, receipt.BatchId, receipt.AcceptedOrderCount, receipt.AcceptedQuantity);

        PersistAccepted(orders);

        return ValueTask.FromResult(receipt);
    }

    /// <inheritdoc />
    public ValueTask<AggregationSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        EnsureRestored();

        var state = ReadState();

        SortByProductId(state.Items);

        long totalQuantity = 0;

        foreach (var item in state.Items)
        {
            totalQuantity += item.TotalQuantity;
        }

        var snapshot = new AggregationSnapshot(
            _timeProvider.GetUtcNow(),
            state.Items.Count,
            totalQuantity,
            state.AcceptedRequestCount,
            state.AcceptedOrderCount,
            state.Items);

        return ValueTask.FromResult(snapshot);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<AggregatedOrderItem>> DrainAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        EnsureRestored();

        List<AggregatedOrderItem> drained;

        lock (_gate)
        {
            drained = CopyItems();
            _products.Clear();
        }

        SortByProductId(drained);

        PersistCleared();

        return ValueTask.FromResult<IReadOnlyList<AggregatedOrderItem>>(drained);
    }

    /// <summary>
    /// Loads previously persisted state, once, before the first operation.
    /// </summary>
    /// <returns>The state to start from, or null to start empty.</returns>
    protected virtual PersistedAggregate? Restore() => null;

    /// <summary>
    /// Called after a request has been accumulated, outside the lock, with only what that
    /// request contributed. One call is one accepted request carrying
    /// <c>orders.Count</c> orders, so a store can maintain the lifetime counters without
    /// being told them.
    /// </summary>
    /// <param name="orders">The orders that were just added to the aggregate.</param>
    protected virtual void PersistAccepted(IReadOnlyList<Order> orders)
    {
    }

    /// <summary>
    /// Called after the aggregate has been handed over and cleared, outside the lock.
    /// </summary>
    /// <remarks>
    /// A store must not lose this: skipping it would let a restart resurrect orders that
    /// have already gone downstream, which breaks the "counted exactly once" invariant.
    /// </remarks>
    protected virtual void PersistCleared()
    {
    }

    /// <summary>
    /// Takes a consistent copy of the current state.
    /// </summary>
    /// <returns>The products and the lifetime counters.</returns>
    private PersistedAggregate ReadState()
    {
        // Only the copy happens under the lock. Sorting and summing hundreds of products
        // while holding it would stall every writer for the duration of the read, which is
        // exactly when a dashboard poll collides with a burst of submissions.
        lock (_gate)
        {
            return new PersistedAggregate(CopyItems(), _acceptedRequestCount, _acceptedOrderCount);
        }
    }

    private void EnsureRestored()
    {
        lock (_gate)
        {
            if (_restored)
            {
                return;
            }

            // Set before the call, so a store whose Restore throws fails that one request
            // and then runs empty, instead of replaying the same failure forever. The
            // failure is not swallowed: the caller sees it, which is what makes a broken
            // store visible rather than silently starting from nothing.
            _restored = true;

            var state = Restore();

            if (state is null)
            {
                return;
            }

            foreach (var item in state.Items)
            {
                _products[item.ProductId] = ProductAccumulator.FromAggregate(item);
            }

            _acceptedRequestCount = state.AcceptedRequestCount;
            _acceptedOrderCount = state.AcceptedOrderCount;
        }
    }

    private List<AggregatedOrderItem> CopyItems()
    {
        var items = new List<AggregatedOrderItem>(_products.Count);

        foreach (var (productId, accumulator) in _products)
        {
            items.Add(accumulator.ToAggregate(productId));
        }

        return items;
    }

    private static void SortByProductId(List<AggregatedOrderItem> items) =>
        items.Sort(static (left, right) => string.CompareOrdinal(left.ProductId, right.ProductId));

    /// <summary>
    /// Mutable counters for a single product. All members are accessed under the owner lock.
    /// </summary>
    private sealed class ProductAccumulator
    {
        private readonly DateTimeOffset _firstSeenUtc;
        private long _totalQuantity;
        private int _orderCount;
        private DateTimeOffset _lastUpdatedUtc;

        public ProductAccumulator(DateTimeOffset firstSeenUtc)
        {
            _firstSeenUtc = firstSeenUtc;
            _lastUpdatedUtc = firstSeenUtc;
        }

        private ProductAccumulator(
            DateTimeOffset firstSeenUtc,
            DateTimeOffset lastUpdatedUtc,
            long totalQuantity,
            int orderCount)
        {
            _firstSeenUtc = firstSeenUtc;
            _lastUpdatedUtc = lastUpdatedUtc;
            _totalQuantity = totalQuantity;
            _orderCount = orderCount;
        }

        public static ProductAccumulator FromAggregate(AggregatedOrderItem item) =>
            new(item.FirstSeenUtc, item.LastUpdatedUtc, item.TotalQuantity, item.OrderCount);

        public void Add(int quantity, DateTimeOffset timestampUtc)
        {
            _totalQuantity += quantity;
            _orderCount++;
            _lastUpdatedUtc = timestampUtc;
        }

        public AggregatedOrderItem ToAggregate(string productId) =>
            new(productId, _totalQuantity, _orderCount, _firstSeenUtc, _lastUpdatedUtc);
    }
}

/// <summary>
/// Everything a store has to hand back for the aggregate to survive a restart.
/// </summary>
/// <param name="Items">The accumulated products.</param>
/// <param name="AcceptedRequestCount">Requests accepted since the counters began.</param>
/// <param name="AcceptedOrderCount">Orders accepted since the counters began.</param>
public sealed record PersistedAggregate(
    List<AggregatedOrderItem> Items,
    long AcceptedRequestCount,
    long AcceptedOrderCount);
