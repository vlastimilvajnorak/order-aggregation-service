namespace OrderAggregationService.Services;

/// <summary>
/// Names the persistence provider backing the aggregate.
/// </summary>
/// <remarks>
/// The specification requires the persistence mechanism to be selectable through
/// configuration. This enum is that selector: it is bound from
/// <see cref="OrderPersistenceOptions"/> and resolved in
/// <see cref="OrderAggregationServiceCollectionExtensions.AddOrderAggregation"/>.
/// </remarks>
public enum OrderStorageType
{
    /// <summary>
    /// Process-local accumulation. Fastest, and lost on restart. The specification
    /// states this is sufficient, so it is the only provider that is implemented.
    /// </summary>
    InMemory = 0,

    /// <summary>
    /// A relational database. Present to demonstrate how a provider is selected and
    /// configured; <see cref="DatabaseOrderAggregator"/> is a skeleton, not an
    /// implementation.
    /// </summary>
    Database = 1,
}
