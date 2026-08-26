using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

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

        OrderRequest[] batch = [new("456", 5), new("789", 42)];

        using var response = await client.PostAsJsonAsync(OrdersPath, batch);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var receipt = await response.Content.ReadFromJsonAsync<OrderBatchReceipt>();

        Assert.NotNull(receipt);
        Assert.NotEqual(Guid.Empty, receipt.BatchId);
        Assert.Equal(2, receipt.AcceptedOrderCount);
        Assert.Equal(47, receipt.AcceptedQuantity);
    }

    [Fact]
    public async Task PostOrders_ThenReadAggregates_ReturnsAccumulatedTotals()
    {
        using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();

        using var first = await client.PostAsJsonAsync(
            OrdersPath,
            new OrderRequest[] { new("456", 5), new("789", 42) });
        using var second = await client.PostAsJsonAsync(
            OrdersPath,
            new OrderRequest[] { new("456", 3) });

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);

        var snapshot = await client.GetFromJsonAsync<AggregationSnapshot>(AggregatesPath);

        Assert.NotNull(snapshot);
        Assert.Equal(2, snapshot.ProductCount);
        Assert.Equal(50, snapshot.TotalQuantity);
        Assert.Equal(2, snapshot.AcceptedRequestCount);

        var product456 = Assert.Single(snapshot.Items, item => item.ProductId == "456");
        Assert.Equal(8, product456.TotalQuantity);
        Assert.Equal(2, product456.OrderCount);
    }

    [Fact]
    public async Task PostOrders_WithEmptyBatch_ReturnsValidationProblem()
    {
        using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(OrdersPath, Array.Empty<OrderRequest>());

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
            new OrderRequest[] { new("456", 0) });

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
            new OrderRequest[] { new(null, 5) });

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
            new OrderRequest[] { new("456", 5), new("789", 0) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var snapshot = await client.GetFromJsonAsync<AggregationSnapshot>(AggregatesPath);

        Assert.NotNull(snapshot);
        Assert.Empty(snapshot.Items);
        Assert.Equal(0, snapshot.AcceptedRequestCount);
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
    public async Task GetSwaggerUi_IsServed()
    {
        using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetOpenApiDocument_DocumentsEveryEndpoint()
    {
        using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();

        using var document = await client.GetFromJsonAsync<JsonDocument>("/openapi/v1.json")
            ?? throw new InvalidOperationException("The OpenAPI document was empty.");

        var paths = document.RootElement.GetProperty("paths");

        foreach (var (path, method) in new[]
        {
            ("/api/orders", "post"),
            ("/api/orders/aggregates", "get"),
            ("/health", "get"),
        })
        {
            var operation = paths.GetProperty(path).GetProperty(method);

            Assert.False(
                string.IsNullOrWhiteSpace(operation.GetProperty("summary").GetString()),
                $"{method.ToUpperInvariant()} {path} has no summary.");

            foreach (var response in operation.GetProperty("responses").EnumerateObject())
            {
                var description = response.Value.GetProperty("description").GetString();

                Assert.False(
                    string.IsNullOrWhiteSpace(description),
                    $"{method.ToUpperInvariant()} {path} response {response.Name} has no description.");

                // The generator falls back to the reason phrase when nothing is documented.
                Assert.NotEqual("OK", description);
                Assert.NotEqual("Accepted", description);
                Assert.NotEqual("Bad Request", description);
            }
        }
    }

    [Theory]
    [InlineData("[{\"productId\":")]          // truncated
    [InlineData("{\"productId\":\"456\"}")]   // an object where an array is required
    [InlineData("[{\"productId\":\"456\",\"quantity\":\"many\"}]")]  // wrong value type
    [InlineData("[{\"productId\":\"456\",\"quantity\":99999999999}]")] // outside int range
    public async Task PostOrders_WithABodyTheServerCannotRead_ReturnsBadRequestNotServerError(string body)
    {
        using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();

        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(OrdersPath, content);

        // A body the caller got wrong is the caller's fault. Reporting it as 500 would
        // both mislead the caller and bury real server faults in the logs.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.False(
            string.IsNullOrWhiteSpace(problem.GetProperty("detail").GetString()),
            "The response must say what was wrong with the body.");
        Assert.False(
            string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()),
            "The response must be correlatable with the server logs.");
    }

    [Fact]
    public async Task PostOrders_WithoutJsonContentType_ReturnsUnsupportedMediaTypeWithAProblemBody()
    {
        using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();

        using var content = new StringContent("[]", Encoding.UTF8, "text/plain");
        using var response = await client.PostAsync(OrdersPath, content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);

        // The framework produces this status itself, with no body. An API that promises
        // problem details has to keep that promise for responses it did not write.
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(415, problem.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task PostOrders_WithAnOversizedProductId_IsRejectedAndChangesNothing()
    {
        using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();

        var productId = new string('4', 65);

        using var response = await client.PostAsJsonAsync(
            OrdersPath,
            new OrderRequest[] { new(productId, 5) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var aggregates = await client.GetAsync(AggregatesPath);
        var snapshot = await aggregates.Content.ReadFromJsonAsync<AggregationSnapshot>();

        Assert.NotNull(snapshot);
        Assert.Empty(snapshot.Items);
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
