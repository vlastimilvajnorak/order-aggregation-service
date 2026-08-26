using System.Text.Json;

namespace OrderAggregationService.Tests;

/// <summary>
/// Tests the document handed to the downstream system. The payload is the contract,
/// so it is asserted as JSON rather than as an object graph.
/// </summary>
public sealed class ConsoleOrderDispatcherTests
{
    private static readonly DateTimeOffset DispatchedAt = new(2026, 8, 26, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreatePayloadJson_WritesTheAggregatedOrders()
    {
        AggregatedOrderItem[] aggregates =
        [
            new("456", 8, 2, DispatchedAt, DispatchedAt),
            new("789", 42, 1, DispatchedAt, DispatchedAt),
        ];

        using var document = JsonDocument.Parse(
            ConsoleOrderDispatcher.CreatePayloadJson(DispatchedAt, aggregates));
        var root = document.RootElement;

        Assert.Equal(2, root.GetProperty("productCount").GetInt32());
        Assert.Equal(50, root.GetProperty("totalQuantity").GetInt64());

        var items = root.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());

        Assert.Equal("456", items[0].GetProperty("productId").GetString());
        Assert.Equal(8, items[0].GetProperty("quantity").GetInt64());
        Assert.Equal("789", items[1].GetProperty("productId").GetString());
        Assert.Equal(42, items[1].GetProperty("quantity").GetInt64());
    }

    [Fact]
    public void CreatePayloadJson_StampsTheDispatchTime()
    {
        using var document = JsonDocument.Parse(
            ConsoleOrderDispatcher.CreatePayloadJson(DispatchedAt, []));

        Assert.Equal(
            DispatchedAt,
            document.RootElement.GetProperty("dispatchedAtUtc").GetDateTimeOffset());
    }

    [Fact]
    public void CreatePayloadJson_IsASingleLine()
    {
        var json = ConsoleOrderDispatcher.CreatePayloadJson(
            DispatchedAt,
            [new AggregatedOrderItem("456", 1, 1, DispatchedAt, DispatchedAt)]);

        // The payload goes through a log message, so it must not span lines.
        Assert.DoesNotContain('\n', json);
    }
}
