using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OrderAggregationService.Services;

/// <summary>
/// Dependency injection registrations for the order aggregation pipeline.
/// </summary>
public static class OrderAggregationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the aggregation and persistence options, the configured provider, the
    /// dispatch pipeline and the aggregation health check.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">Configuration the options are bound from.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddOrderAggregation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<OrderAggregationOptions>()
            .Bind(configuration.GetSection(OrderAggregationOptions.SectionName))
            .Validate(
                static options => options.DispatchInterval > TimeSpan.Zero,
                "OrderAggregation:DispatchInterval must be greater than zero.")
            .Validate(
                static options => options.MaxOrdersPerRequest > 0,
                "OrderAggregation:MaxOrdersPerRequest must be greater than zero.")
            .Validate(
                static options => options.MaxProductIdLength > 0,
                "OrderAggregation:MaxProductIdLength must be greater than zero.")
            .ValidateOnStart();

        services.AddOptions<OrderPersistenceOptions>()
            .Bind(configuration.GetSection(OrderPersistenceOptions.SectionName))
            .Validate(
                static options => Enum.IsDefined(options.Provider),
                "OrderPersistence:Provider must name a supported provider.")
            .Validate(
                static options => options.Provider == OrderStorageType.InMemory
                    || !string.IsNullOrWhiteSpace(options.ConnectionString),
                "OrderPersistence:ConnectionString is required by the selected provider.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        // The provider is chosen by configuration. Adding one means a branch here, a
        // member on OrderStorageType and a subclass of OrderAggregatorBase; nothing above
        // this line, and no endpoint or component, has to change.
        services.TryAddSingleton<IOrderAggregator>(static provider =>
        {
            var options = provider.GetRequiredService<IOptions<OrderPersistenceOptions>>().Value;

            return options.Provider switch
            {
                OrderStorageType.InMemory =>
                    ActivatorUtilities.CreateInstance<InMemoryOrderAggregator>(provider),
                OrderStorageType.Database =>
                    ActivatorUtilities.CreateInstance<DatabaseOrderAggregator>(provider),
                _ => throw new InvalidOperationException(
                    $"Unsupported {OrderPersistenceOptions.SectionName}:" +
                    $"{nameof(OrderPersistenceOptions.Provider)} value '{options.Provider}'."),
            };
        });

        services.TryAddSingleton<IOrderDispatcher, ConsoleOrderDispatcher>();
        services.TryAddSingleton<IDispatchHistory, InMemoryDispatchHistory>();
        services.AddHostedService<OrderDispatchBackgroundService>();

        services.AddHealthChecks()
            .AddCheck<OrderAggregationHealthCheck>(
                OrderAggregationHealthCheck.Name,
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"]);

        return services;
    }
}
