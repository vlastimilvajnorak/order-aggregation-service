namespace OrderAggregationService.Services;

/// <summary>
/// Configuration of the persistence provider, bound from the <c>OrderPersistence</c>
/// configuration section.
/// </summary>
/// <remarks>
/// Persistence has its own section rather than sharing the aggregation one, because a
/// provider brings settings that mean nothing to the rest of the service - a connection
/// string, a table name. Adding a provider adds properties here and leaves
/// <see cref="OrderAggregationOptions"/> untouched.
/// </remarks>
public sealed class OrderPersistenceOptions
{
    /// <summary>
    /// Name of the configuration section these options are bound from.
    /// </summary>
    public const string SectionName = "OrderPersistence";

    /// <summary>
    /// Gets or sets the provider backing the aggregate. Defaults to
    /// <see cref="OrderStorageType.InMemory"/>.
    /// </summary>
    public OrderStorageType Provider { get; set; } = OrderStorageType.InMemory;

    /// <summary>
    /// Gets or sets the connection string used by a database provider. Required when
    /// <see cref="Provider"/> is not <see cref="OrderStorageType.InMemory"/>, ignored
    /// otherwise.
    /// </summary>
    /// <remarks>
    /// A real deployment supplies this through the environment
    /// (<c>OrderPersistence__ConnectionString</c>) or a secret store, never from a
    /// committed file.
    /// </remarks>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the table a database provider accumulates into.
    /// </summary>
    public string TableName { get; set; } = "OrderAggregates";
}
