namespace OrderAggregationService.Services;

/// <summary>
/// The shape a database-backed provider takes. Deliberately not implemented.
/// </summary>
/// <remarks>
/// This class exists to show what "the persistence mechanism is extensible" means in
/// practice, and how far the extension reaches: it overrides the three hooks of
/// <see cref="OrderAggregatorBase"/> and nothing else. The accumulation semantics, the
/// locking and the counters stay in the base class, so a provider cannot change how
/// quantities aggregate - only where they are kept.
///
/// The specification states that an in-memory implementation is sufficient, so no
/// database implementation, and no data-access dependency, is part of this solution. The
/// hooks throw rather than doing nothing: a provider that silently accumulated in memory
/// while claiming to be durable would be worse than one that fails loudly.
/// </remarks>
public sealed class DatabaseOrderAggregator : OrderAggregatorBase
{
    private readonly OrderPersistenceOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseOrderAggregator"/> class.
    /// </summary>
    /// <param name="options">Provider configuration: connection string and table.</param>
    /// <param name="timeProvider">Clock used to stamp accepted orders.</param>
    /// <param name="logger">Logger for accepted requests.</param>
    public DatabaseOrderAggregator(
        IOptions<OrderPersistenceOptions> options,
        TimeProvider timeProvider,
        ILogger<DatabaseOrderAggregator> logger)
        : base(timeProvider, logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Would read the accumulated rows and the lifetime counters:
    /// <c>SELECT ProductId, TotalQuantity, OrderCount, FirstSeenUtc, LastUpdatedUtc FROM {table}</c>.
    /// </remarks>
    protected override PersistedAggregate? Restore() => throw NotImplemented(nameof(Restore));

    /// <inheritdoc />
    /// <remarks>
    /// Would upsert one row per product in a single round trip, adding rather than
    /// replacing: <c>MERGE ... WHEN MATCHED THEN UPDATE SET TotalQuantity = TotalQuantity + @delta</c>.
    /// Adding is what makes a concurrent request safe without holding a transaction open.
    /// </remarks>
    protected override void PersistAccepted(IReadOnlyList<Order> orders) =>
        throw NotImplemented(nameof(PersistAccepted));

    /// <inheritdoc />
    /// <remarks>
    /// Would delete the accumulated rows in the same transaction that records the
    /// hand-over: <c>DELETE FROM {table}</c>. This one must be durable - skipping it lets
    /// a restart resurrect orders that have already gone downstream.
    /// </remarks>
    protected override void PersistCleared() => throw NotImplemented(nameof(PersistCleared));

    private NotImplementedException NotImplemented(string hook) =>
        new($"{nameof(DatabaseOrderAggregator)}.{hook} is a skeleton showing how a " +
            $"provider plugs in. Implement it against '{_options.TableName}' before " +
            $"selecting {OrderPersistenceOptions.SectionName}:" +
            $"{nameof(OrderPersistenceOptions.Provider)}=" +
            $"{nameof(OrderStorageType.Database)}.");
}
