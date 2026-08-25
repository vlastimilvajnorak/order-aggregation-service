using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OrderAggregationService.Services;

/// <summary>
/// Dependency injection registrations for the order aggregation pipeline.
/// </summary>
public static class OrderAggregationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the aggregation options, the aggregator, the dispatch pipeline and the
    /// aggregation health check.
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
                static options => options.MaxLinesPerRequest > 0,
                "OrderAggregation:MaxLinesPerRequest must be greater than zero.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IOrderAggregator, InMemoryOrderAggregator>();
        services.TryAddSingleton<IOrderDispatcher, LoggingOrderDispatcher>();
        services.AddHostedService<OrderDispatchBackgroundService>();

        services.AddHealthChecks()
            .AddCheck<OrderAggregationHealthCheck>(
                OrderAggregationHealthCheck.Name,
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"]);

        return services;
    }
}
