using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using OrderAggregationService.Models;

namespace OrderAggregationService.Tests;

/// <summary>
/// Integration tests that exercise the HTTP surface of the running application.
/// </summary>
public sealed class OrderApiIntegrationTests
{
    private const string OrdersPath = "/api/orders";
    private const string AggregatesPath = "/api/orders/aggregates";

    [Fact]
    public async Task PostOrders_WithValidBatch_ReturnsAcceptedAndReceipt()
    {
        using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();

        OrderItemRequest[] batch = [new("456", 5), new("789", 42)];

        using var response = await client.PostAsJsonAsync(OrdersPath, batch);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var receipt = await response.Content.ReadFromJsonAsync<OrderBatchReceipt>();

        Assert.NotNull(receipt);
        Assert.NotEqual(Guid.Empty, receipt.BatchId);
        Assert.Equal(2, receipt.AcceptedLineCount);
        Assert.Equal(47, receipt.AcceptedQuantity);
    }

    [Fact]
    public async Task PostOrders_ThenReadAggregates_ReturnsAccumulatedTotals()
    {
        using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();

        using var first = await client.PostAsJsonAsync(
            OrdersPath,
            new OrderItemRequest[] { new("456", 5), new("789", 42) });
        using var second = await client.PostAsJsonAsync(
            OrdersPath,
            new OrderItemRequest[] { new("456", 3) });

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);

        var snapshot = await client.GetFromJsonAsync<AggregationSnapshot>(AggregatesPath);

        Assert.NotNull(snapshot);
        Assert.Equal(2, snapshot.ProductCount);
        Assert.Equal(50, snapshot.TotalQuantity);
        Assert.Equal(2, snapshot.AcceptedBatchCount);

        var product456 = Assert.Single(snapshot.Items, item => item.ProductId == "456");
        Assert.Equal(8, product456.TotalQuantity);
        Assert.Equal(2, product456.LineCount);
    }

    [Fact]
    public async Task PostOrders_WithEmptyBatch_ReturnsValidationProblem()
    {
        using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(OrdersPath, Array.Empty<OrderItemRequest>());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains("request", problem.Errors.Keys);
    }

    [Fact]
    public async Task PostOrders_WithNonPositiveQuantity_ReturnsValidationProblem()
    {
        using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            OrdersPath,
            new OrderItemRequest[] { new("456", 0) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains("[0].quantity", problem.Errors.Keys);
    }

    [Fact]
    public async Task PostOrders_WithMissingProductId_ReturnsValidationProblem()
    {
        using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            OrdersPath,
            new OrderItemRequest[] { new(null, 5) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains("[0].productId", problem.Errors.Keys);
    }

    [Fact]
    public async Task PostOrders_RejectedBatch_IsNotAggregated()
    {
        using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            OrdersPath,
            new OrderItemRequest[] { new("456", 5), new("789", 0) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var snapshot = await client.GetFromJsonAsync<AggregationSnapshot>(AggregatesPath);

        Assert.NotNull(snapshot);
        Assert.Empty(snapshot.Items);
        Assert.Equal(0, snapshot.AcceptedBatchCount);
    }

    [Fact]
    public async Task GetHealth_ReportsHealthy()
    {
        using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadAsStringAsync();

        Assert.Contains("Healthy", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetOpenApiDocument_IsServed()
    {
        using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var document = await response.Content.ReadAsStringAsync();

        Assert.Contains("/api/orders", document, StringComparison.Ordinal);
    }
}
