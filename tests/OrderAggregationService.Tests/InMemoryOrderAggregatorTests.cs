using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace OrderAggregationService.Tests;

/// <summary>
/// Unit tests for the in-memory aggregation implementation. Every test builds its own
/// aggregator, so the tests share no state and may run in any order.
/// </summary>
public sealed class InMemoryOrderAggregatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AggregateAsync_SameProductInOneBatch_SumsQuantities()
    {
        var aggregator = CreateAggregator();

        await aggregator.AggregateAsync([new Order("456", 5), new Order("456", 42)]);

        var snapshot = await aggregator.GetSnapshotAsync();
        var product = Assert.Single(snapshot.Items);

        Assert.Equal("456", product.ProductId);
        Assert.Equal(47, product.TotalQuantity);
        Assert.Equal(2, product.OrderCount);
    }

    [Fact]
    public async Task AggregateAsync_SameProductAcrossBatches_SumsQuantities()
    {
        var aggregator = CreateAggregator();

        await aggregator.AggregateAsync([new Order("456", 5)]);
        await aggregator.AggregateAsync([new Order("456", 7)]);
        await aggregator.AggregateAsync([new Order("456", 1)]);

        var snapshot = await aggregator.GetSnapshotAsync();
        var product = Assert.Single(snapshot.Items);

        Assert.Equal(13, product.TotalQuantity);
        Assert.Equal(3, product.OrderCount);
        Assert.Equal(3, snapshot.AcceptedRequestCount);
        Assert.Equal(3, snapshot.AcceptedOrderCount);
    }

    [Fact]
    public async Task AggregateAsync_DifferentProducts_AreAggregatedSeparately()
    {
        var aggregator = CreateAggregator();

        await aggregator.AggregateAsync(
        [
            new Order("456", 5),
            new Order("789", 42),
            new Order("456", 3),
        ]);

        var snapshot = await aggregator.GetSnapshotAsync();

        Assert.Equal(2, snapshot.ProductCount);
        Assert.Equal(50, snapshot.TotalQuantity);

        var first = snapshot.Items[0];
        var second = snapshot.Items[1];

        Assert.Equal("456", first.ProductId);
        Assert.Equal(8, first.TotalQuantity);
        Assert.Equal("789", second.ProductId);
        Assert.Equal(42, second.TotalQuantity);
    }

    [Fact]
    public async Task AggregateAsync_StampsFirstSeenAndLastUpdated()
    {
        var timeProvider = new FakeTimeProvider(Now);
        var aggregator = CreateAggregator(timeProvider);

        await aggregator.AggregateAsync([new Order("456", 1)]);
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        await aggregator.AggregateAsync([new Order("456", 1)]);

        var snapshot = await aggregator.GetSnapshotAsync();
        var product = Assert.Single(snapshot.Items);

        Assert.Equal(Now, product.FirstSeenUtc);
        Assert.Equal(Now.AddMinutes(5), product.LastUpdatedUtc);
    }

    [Fact]
    public async Task GetSnapshotAsync_OnFreshAggregator_IsEmpty()
    {
        var aggregator = CreateAggregator();

        var snapshot = await aggregator.GetSnapshotAsync();

        Assert.Empty(snapshot.Items);
        Assert.Equal(0, snapshot.ProductCount);
        Assert.Equal(0, snapshot.TotalQuantity);
        Assert.Equal(0, snapshot.AcceptedRequestCount);
    }

    [Fact]
    public async Task DrainAsync_ReturnsAndClearsEverything()
    {
        var aggregator = CreateAggregator();
        await aggregator.AggregateAsync([new Order("456", 5), new Order("789", 42)]);

        var drained = await aggregator.DrainAsync();

        Assert.Equal(2, drained.Count);
        Assert.Equal(47, drained.Sum(item => item.TotalQuantity));

        var snapshot = await aggregator.GetSnapshotAsync();

        Assert.Empty(snapshot.Items);

        // Lifetime counters describe everything ever accepted, so draining must not reset them.
        Assert.Equal(1, snapshot.AcceptedRequestCount);
        Assert.Equal(2, snapshot.AcceptedOrderCount);
    }

    [Fact]
    public async Task AggregateAsync_UnderConcurrency_KeepsExactTotals()
    {
        var aggregator = CreateAggregator();
        const int WriterCount = 16;
        const int BatchesPerWriter = 50;

        var writers = Enumerable.Range(0, WriterCount).Select(_ => Task.Run(async () =>
        {
            for (var batch = 0; batch < BatchesPerWriter; batch++)
            {
                await aggregator.AggregateAsync([new Order("456", 1), new Order("789", 2)]);
            }
        }));

        await Task.WhenAll(writers);

        var snapshot = await aggregator.GetSnapshotAsync();

        Assert.Equal(2, snapshot.ProductCount);
        Assert.Equal(WriterCount * BatchesPerWriter, snapshot.Items[0].TotalQuantity);
        Assert.Equal(WriterCount * BatchesPerWriter * 2, snapshot.Items[1].TotalQuantity);
        Assert.Equal(WriterCount * BatchesPerWriter, snapshot.AcceptedRequestCount);
        Assert.Equal(WriterCount * BatchesPerWriter * 2, snapshot.AcceptedOrderCount);
    }

    private static InMemoryOrderAggregator CreateAggregator(TimeProvider? timeProvider = null) =>
        new(timeProvider ?? new FakeTimeProvider(Now), NullLogger<InMemoryOrderAggregator>.Instance);
}
