using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OrderAggregationService.Tests;

/// <summary>
/// The specification requires the persistence provider to be selectable through
/// configuration. These tests pin that behaviour, including the failures when an
/// unsupported provider is named or a selected one is not configured.
/// </summary>
public sealed class OrderAggregationRegistrationTests
{
    [Fact]
    public void AddOrderAggregation_WithoutConfiguration_UsesTheInMemoryStore()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>());

        Assert.IsType<InMemoryOrderAggregator>(provider.GetRequiredService<IOrderAggregator>());
    }

    [Fact]
    public void AddOrderAggregation_WithInMemoryConfigured_ResolvesTheInMemoryStore()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["OrderPersistence:Provider"] = nameof(OrderStorageType.InMemory),
        });

        var options = provider.GetRequiredService<IOptions<OrderPersistenceOptions>>().Value;

        Assert.Equal(OrderStorageType.InMemory, options.Provider);
        Assert.IsType<InMemoryOrderAggregator>(provider.GetRequiredService<IOrderAggregator>());
    }

    [Fact]
    public void AddOrderAggregation_WithAnotherProviderConfigured_ResolvesThatProvider()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["OrderPersistence:Provider"] = nameof(OrderStorageType.Database),
            ["OrderPersistence:ConnectionString"] = "Server=.;Database=orders;",
        });

        // Switching providers is a configuration value, not a code change: nothing above
        // IOrderAggregator sees the difference.
        Assert.IsType<DatabaseOrderAggregator>(provider.GetRequiredService<IOrderAggregator>());
    }

    [Fact]
    public void AddOrderAggregation_WithAProviderThatNeedsAConnection_FailsValidation()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["OrderPersistence:Provider"] = nameof(OrderStorageType.Database),
        });

        var options = provider.GetRequiredService<IOptions<OrderPersistenceOptions>>();
        var failure = Assert.Throws<OptionsValidationException>(() => options.Value);

        Assert.Contains("ConnectionString", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddOrderAggregation_ReadsTheDispatchSettings()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["OrderAggregation:DispatchInterval"] = "00:00:20",
            ["OrderAggregation:DispatchEnabled"] = "true",
        });

        var options = provider.GetRequiredService<IOptions<OrderAggregationOptions>>().Value;

        Assert.Equal(TimeSpan.FromSeconds(20), options.DispatchInterval);
        Assert.True(options.DispatchEnabled);
    }

    [Fact]
    public void AddOrderAggregation_WithAnUnparsableProvider_FailsAtStartup()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["OrderPersistence:Provider"] = "NotAStore",
        });

        var options = provider.GetRequiredService<IOptions<OrderPersistenceOptions>>();

        // A string that is not an enum member never reaches validation: the binder
        // itself throws, so the misconfiguration cannot be silently ignored either.
        var failure = Assert.ThrowsAny<InvalidOperationException>(() => options.Value);

        Assert.Contains("Provider", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddOrderAggregation_WithAnUnsupportedProvider_FailsValidation()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["OrderPersistence:Provider"] = "99",
        });

        var options = provider.GetRequiredService<IOptions<OrderPersistenceOptions>>();
        var failure = Assert.Throws<OptionsValidationException>(() => options.Value);

        Assert.Contains("Provider", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddOrderAggregation_WithANonPositiveInterval_FailsValidation()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["OrderAggregation:DispatchInterval"] = "00:00:00",
        });

        var options = provider.GetRequiredService<IOptions<OrderAggregationOptions>>();
        var failure = Assert.Throws<OptionsValidationException>(() => options.Value);

        Assert.Contains("DispatchInterval", failure.Message, StringComparison.Ordinal);
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOrderAggregation(configuration);

        return services.BuildServiceProvider();
    }
}
