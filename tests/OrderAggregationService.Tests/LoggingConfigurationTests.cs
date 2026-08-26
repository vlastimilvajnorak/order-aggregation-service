using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.Debug;

namespace OrderAggregationService.Tests;

/// <summary>
/// The console logger writes through a bounded background queue, and its default
/// behaviour when that queue fills is to block the producer. Under a flood of orders a
/// slow console would then stall request threads, which is observable as a frozen UI.
/// This pins the configuration that makes the logger shed instead of block.
/// </summary>
public sealed class LoggingConfigurationTests
{
    [Fact]
    public void ConsoleLogging_WhenTheQueueIsFull_DropsInsteadOfBlockingCallers()
    {
        using var factory = new OrderApiFactory();

        var options = factory.Services
            .GetRequiredService<IOptionsMonitor<ConsoleLoggerOptions>>()
            .CurrentValue;

        Assert.Equal(ConsoleLoggerQueueFullMode.DropWrite, options.QueueFullMode);
        Assert.Equal(4096, options.MaxQueueLength);
    }

    [Fact]
    public void Logging_OutsideDevelopment_UsesOnlyTheQueuedConsoleProvider()
    {
        using var factory = new OrderApiFactory();

        var providers = factory.Services.GetServices<ILoggerProvider>().ToList();

        // The default builder would also add the Debug provider, which writes
        // synchronously with no queue and stalls the process under a debugger.
        var provider = Assert.Single(providers);
        Assert.IsType<ConsoleLoggerProvider>(provider);
    }

    [Fact]
    public void Logging_InDevelopment_KeepsTheDebugProviderForTheIde()
    {
        using var factory = new OrderApiFactory();
        using var development = factory.WithWebHostBuilder(
            static builder => builder.UseEnvironment("Development"));

        var providers = development.Services.GetServices<ILoggerProvider>().ToList();

        // Debugging needs the full picture in the VS Output window, so Development
        // accepts the synchronous Debug provider on top of the queued console.
        Assert.Contains(providers, static provider => provider is ConsoleLoggerProvider);
        Assert.Contains(providers, static provider => provider is DebugLoggerProvider);
    }
}
