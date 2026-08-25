using OrderAggregationService.Endpoints;
using OrderAggregationService.Models;

namespace OrderAggregationService.Tests;

/// <summary>
/// Unit tests for the request validation rules.
/// </summary>
public sealed class OrderBatchValidatorTests
{
    private const int MaxLines = 1000;

    [Fact]
    public void Validate_ValidBatch_ProjectsEveryLine()
    {
        OrderItemRequest[] request =
        [
            new("456", 5),
            new("789", 42),
        ];

        var result = OrderBatchValidator.Validate(request, MaxLines);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Lines.Count);
        Assert.Equal("456", result.Lines[0].ProductId);
        Assert.Equal(5, result.Lines[0].Quantity);
        Assert.Equal("789", result.Lines[1].ProductId);
        Assert.Equal(42, result.Lines[1].Quantity);
    }

    [Fact]
    public void Validate_NullRequest_IsRejected()
    {
        var result = OrderBatchValidator.Validate(null, MaxLines);

        Assert.False(result.IsValid);
        Assert.Contains("request", result.Errors.Keys);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Validate_EmptyRequest_IsRejected()
    {
        var result = OrderBatchValidator.Validate([], MaxLines);

        Assert.False(result.IsValid);
        Assert.Contains("request", result.Errors.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingProductId_IsRejected(string? productId)
    {
        var result = OrderBatchValidator.Validate([new OrderItemRequest(productId, 5)], MaxLines);

        Assert.False(result.IsValid);
        Assert.Contains("[0].productId", result.Errors.Keys);
        Assert.Empty(result.Lines);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Validate_NonPositiveQuantity_IsRejected(int quantity)
    {
        var result = OrderBatchValidator.Validate([new OrderItemRequest("456", quantity)], MaxLines);

        Assert.False(result.IsValid);
        Assert.Contains("[0].quantity", result.Errors.Keys);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Validate_ReportsEveryOffendingLineSeparately()
    {
        OrderItemRequest[] request =
        [
            new("456", 5),
            new(null, 3),
            new("789", 0),
        ];

        var result = OrderBatchValidator.Validate(request, MaxLines);

        Assert.False(result.IsValid);
        Assert.Contains("[1].productId", result.Errors.Keys);
        Assert.Contains("[2].quantity", result.Errors.Keys);
        Assert.DoesNotContain("[0].productId", result.Errors.Keys);
    }

    [Fact]
    public void Validate_BatchLargerThanTheLimit_IsRejected()
    {
        var request = Enumerable.Range(0, 4)
            .Select(index => new OrderItemRequest($"product-{index}", 1))
            .ToArray();

        var result = OrderBatchValidator.Validate(request, maxLinesPerRequest: 3);

        Assert.False(result.IsValid);
        Assert.Contains("request", result.Errors.Keys);
    }

    [Fact]
    public void Validate_BatchExactlyAtTheLimit_IsAccepted()
    {
        var request = Enumerable.Range(0, 3)
            .Select(index => new OrderItemRequest($"product-{index}", 1))
            .ToArray();

        var result = OrderBatchValidator.Validate(request, maxLinesPerRequest: 3);

        Assert.True(result.IsValid);
        Assert.Equal(3, result.Lines.Count);
    }
}
