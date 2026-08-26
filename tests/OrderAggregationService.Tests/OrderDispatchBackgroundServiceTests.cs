using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace OrderAggregationService.Tests;

/// <summary>
/// Tests for the hand-over cadence. The specification requires that aggregated orders
/// leave no more often than once every 20 seconds, so these tests drive a controlled
/// clock rather than the wall clock.
/// </summary>
public sealed class OrderDispatchBackgroundServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 8, 0, 0, TimeSpan.Zero);

    // A backstop against a broken cycle. It is never reached while the cycle works.
    private static readonly TimeSpan DispatchTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public void Defaults_MatchTheSpecification()
    {
        var options = new OrderAggregationOptions();

        Assert.Equal(TimeSpan.FromSeconds(20), options.DispatchInterval);
        Assert.True(options.DispatchEnabled);
    }

    [Fact]
    public async Task ExecuteAsync_BeforeTheIntervalElapses_DoesNotDispatch()
    {
        using var harness = Harness.Create();

        await harness.StartAsync();
        await harness.Aggregator.AggregateAsync([new Order("456", 5)]);

        harness.Time.Advance(TimeSpan.FromSeconds(19));

        Assert.Empty(harness.Dispatcher.Batches);
    }

    [Fact]
    public async Task ExecuteAsync_BurstWithinOneInterval_DispatchesOnceWithSummedQuantities()
    {
        using var harness = Harness.Create();

        await harness.StartAsync();

        // A burst inside the window must not shorten the interval.
        await harness.Aggregator.AggregateAsync([new Order("456", 5), new Order("789", 42)]);
        harness.Time.Advance(TimeSpan.FromSeconds(10));
        await harness.Aggregator.AggregateAsync([new Order("456", 3)]);

        Assert.Empty(harness.Dispatcher.Batches);

        harness.Time.Advance(TimeSpan.FromSeconds(10));
        harness.Dispatcher.WaitForDispatch(DispatchTimeout);

        var batch = Assert.Single(harness.Dispatcher.Batches);
        Assert.Equal(2, batch.Count);

        var product456 = Assert.Single(batch, item => item.ProductId == "456");
        Assert.Equal(8, product456.TotalQuantity);

        var product789 = Assert.Single(batch, item => item.ProductId == "789");
        Assert.Equal(42, product789.TotalQuantity);
    }

    [Fact]
    public async Task ExecuteAsync_SecondInterval_DispatchesOnlyWhatArrivedSince()
    {
        using var harness = Harness.Create();

        await harness.StartAsync();

        await harness.Aggregator.AggregateAsync([new Order("456", 5)]);
        harness.Time.Advance(TimeSpan.FromSeconds(20));
        harness.Dispatcher.WaitForDispatch(DispatchTimeout);

        await harness.Aggregator.AggregateAsync([new Order("789", 7)]);
        harness.Time.Advance(TimeSpan.FromSeconds(20));
        harness.Dispatcher.WaitForDispatch(DispatchTimeout);

        var batches = harness.Dispatcher.Batches;
        Assert.Equal(2, batches.Count);

        var firstBatch = Assert.Single(batches[0]);
        Assert.Equal("456", firstBatch.ProductId);

        var secondBatch = Assert.Single(batches[1]);
        Assert.Equal("789", secondBatch.ProductId);
        Assert.Equal(7, secondBatch.TotalQuantity);
    }

    [Fact]
    public async Task ExecuteAsync_RecordsEachHandOverSoItSurvivesTheDrain()
    {
        using var harness = Harness.Create();

        await harness.StartAsync();

        await harness.Aggregator.AggregateAsync([new Order("456", 5), new Order("789", 7)]);
        harness.Time.Advance(TimeSpan.FromSeconds(20));
        harness.History.WaitForRecord(DispatchTimeout);

        var recorded = Assert.Single(harness.History.GetRecent());

        Assert.Equal(2, recorded.ProductCount);
        Assert.Equal(12, recorded.TotalQuantity);

        // The aggregate is empty now, so the history is the only remaining evidence.
        var snapshot = await harness.Aggregator.GetSnapshotAsync();
        Assert.Empty(snapshot.Items);
    }

    [Fact]
    public async Task ExecuteAsync_KeepsHandOversNewestFirst()
    {
        using var harness = Harness.Create();

        await harness.StartAsync();

        await harness.Aggregator.AggregateAsync([new Order("456", 5)]);
        harness.Time.Advance(TimeSpan.FromSeconds(20));
        harness.History.WaitForRecord(DispatchTimeout);

        await harness.Aggregator.AggregateAsync([new Order("789", 9)]);
        harness.Time.Advance(TimeSpan.FromSeconds(20));
        harness.History.WaitForRecord(DispatchTimeout);

        var recent = harness.History.GetRecent();

        Assert.Equal(2, recent.Count);
        Assert.Equal(9, recent[0].TotalQuantity);
        Assert.Equal(5, recent[1].TotalQuantity);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDispatchIsDisabled_NeverDispatches()
    {
        using var harness = Harness.Create(dispatchEnabled: false);

        await harness.StartAsync();
        await harness.Aggregator.AggregateAsync([new Order("456", 5)]);

        harness.Time.Advance(TimeSpan.FromMinutes(5));

        Assert.Empty(harness.Dispatcher.Batches);
    }

    private sealed class Harness : IDisposable
    {
        private Harness(
            OrderDispatchBackgroundService service,
            IOrderAggregator aggregator,
            RecordingOrderDispatcher dispatcher,
            RecordingDispatchHistory history,
            ArmedFakeTimeProvider time,
            OrderAggregationOptions options)
        {
            Service = service;
            Aggregator = aggregator;
            Dispatcher = dispatcher;
            History = history;
            Time = time;
            Settings = options;
        }

        public OrderDispatchBackgroundService Service { get; }

        public IOrderAggregator Aggregator { get; }

        public RecordingOrderDispatcher Dispatcher { get; }

        public RecordingDispatchHistory History { get; }

        public ArmedFakeTimeProvider Time { get; }

        public OrderAggregationOptions Settings { get; }

        public static Harness Create(bool dispatchEnabled = true)
        {
            var time = new ArmedFakeTimeProvider(Now);
            var aggregator = new InMemoryOrderAggregator(
                time,
                NullLogger<InMemoryOrderAggregator>.Instance);
            var dispatcher = new RecordingOrderDispatcher();
            var history = new RecordingDispatchHistory();

            var settings = new OrderAggregationOptions
            {
                DispatchEnabled = dispatchEnabled,
                DispatchInterval = TimeSpan.FromSeconds(20),
            };

            var service = new OrderDispatchBackgroundService(
                aggregator,
                dispatcher,
                history,
                Options.Create(settings),
                time,
                NullLogger<OrderDispatchBackgroundService>.Instance);

            return new Harness(service, aggregator, dispatcher, history, time, settings);
        }

        /// <summary>
        /// Starts the service and waits until it has armed its dispatch timer, so the
        /// clock cannot be advanced past the first tick before it exists.
        /// </summary>
        /// <returns>A task that completes once the service is running.</returns>
        public async Task StartAsync()
        {
            await Service.StartAsync(CancellationToken.None);

            if (Settings.DispatchEnabled)
            {
                Time.WaitUntilArmed(TimeSpan.FromSeconds(30));
            }
        }

        public void Dispose()
        {
            Service.Dispose();
            Dispatcher.Dispose();
            History.Dispose();
            Time.Dispose();
        }
    }
}
