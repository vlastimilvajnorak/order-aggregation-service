using OrderAggregationService.Endpoints;

namespace OrderAggregationService.Tests;

/// <summary>
/// Unit tests for the request validation rules.
/// </summary>
public sealed class OrderBatchValidatorTests
{
    private const int MaxOrders = 1000;

    private const int MaxProductIdLength = 64;

    [Fact]
    public void Validate_AProductIdOverTheLimit_IsRejected()
    {
        // Every distinct identifier costs an entry until the next hand-over, so the length
        // of what a caller sends must not be what decides how much the service holds.
        var productId = new string('4', MaxProductIdLength + 1);

        var result = OrderBatchValidator.Validate(
            [new OrderRequest(productId, 5)], MaxOrders, MaxProductIdLength);

        Assert.False(result.IsValid);
        Assert.Contains("[0].productId", result.Errors.Keys);
    }

    [Fact]
    public void Validate_AProductIdAtTheLimit_IsAccepted()
    {
        var productId = new string('4', MaxProductIdLength);

        var result = OrderBatchValidator.Validate(
            [new OrderRequest(productId, 5)], MaxOrders, MaxProductIdLength);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ValidBatch_ProjectsEveryOrder()
    {
        OrderRequest[] request =
        [
            new("456", 5),
            new("789", 42),
        ];

        var result = OrderBatchValidator.Validate(request, MaxOrders, MaxProductIdLength);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Orders.Count);
        Assert.Equal("456", result.Orders[0].ProductId);
        Assert.Equal(5, result.Orders[0].Quantity);
        Assert.Equal("789", result.Orders[1].ProductId);
        Assert.Equal(42, result.Orders[1].Quantity);
    }

    [Fact]
    public void Validate_NullRequest_IsRejected()
    {
        var result = OrderBatchValidator.Validate(null, MaxOrders, MaxProductIdLength);

        Assert.False(result.IsValid);
        Assert.Contains("request", result.Errors.Keys);
        Assert.Empty(result.Orders);
    }

    [Fact]
    public void Validate_EmptyRequest_IsRejected()
    {
        var result = OrderBatchValidator.Validate([], MaxOrders, MaxProductIdLength);

        Assert.False(result.IsValid);
        Assert.Contains("request", result.Errors.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingProductId_IsRejected(string? productId)
    {
        var result = OrderBatchValidator.Validate([new OrderRequest(productId, 5)], MaxOrders, MaxProductIdLength);

        Assert.False(result.IsValid);
        Assert.Contains("[0].productId", result.Errors.Keys);
        Assert.Empty(result.Orders);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Validate_NonPositiveQuantity_IsRejected(int quantity)
    {
        var result = OrderBatchValidator.Validate([new OrderRequest("456", quantity)], MaxOrders, MaxProductIdLength);

        Assert.False(result.IsValid);
        Assert.Contains("[0].quantity", result.Errors.Keys);
        Assert.Empty(result.Orders);
    }

    [Fact]
    public void Validate_ReportsEveryOffendingLineSeparately()
    {
        OrderRequest[] request =
        [
            new("456", 5),
            new(null, 3),
            new("789", 0),
        ];

        var result = OrderBatchValidator.Validate(request, MaxOrders, MaxProductIdLength);

        Assert.False(result.IsValid);
        Assert.Contains("[1].productId", result.Errors.Keys);
        Assert.Contains("[2].quantity", result.Errors.Keys);
        Assert.DoesNotContain("[0].productId", result.Errors.Keys);
    }

    [Fact]
    public void Validate_BatchLargerThanTheLimit_IsRejected()
    {
        var request = Enumerable.Range(0, 4)
            .Select(index => new OrderRequest($"product-{index}", 1))
            .ToArray();

        var result = OrderBatchValidator.Validate(request, maxOrdersPerRequest: 3, MaxProductIdLength);

        Assert.False(result.IsValid);
        Assert.Contains("request", result.Errors.Keys);
    }

    [Fact]
    public void Validate_BatchExactlyAtTheLimit_IsAccepted()
    {
        var request = Enumerable.Range(0, 3)
            .Select(index => new OrderRequest($"product-{index}", 1))
            .ToArray();

        var result = OrderBatchValidator.Validate(request, maxOrdersPerRequest: 3, MaxProductIdLength);

        Assert.True(result.IsValid);
        Assert.Equal(3, result.Orders.Count);
    }
}
