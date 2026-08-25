using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using OrderAggregationService.Models;
using OrderAggregationService.Services;

namespace OrderAggregationService.Endpoints;

/// <summary>
/// HTTP endpoints of the order aggregation API.
/// </summary>
public static class OrderEndpoints
{
    /// <summary>
    /// Route of the order submission endpoint.
    /// </summary>
    public const string OrdersPath = "/api/orders";

    /// <summary>
    /// Route of the aggregate read endpoint.
    /// </summary>
    public const string AggregatesPath = "/api/orders/aggregates";

    private const string LoggerCategory = "OrderAggregationService.Endpoints.Orders";

    /// <summary>
    /// Maps the order endpoints onto the application.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same endpoint route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost(OrdersPath, SubmitOrdersAsync)
            .WithTags("Orders")
            .WithName("SubmitOrders")
            .WithSummary("Submits a batch of order lines for aggregation.")
            .WithDescription(
                "Accepts a JSON array of order lines. Quantities are accumulated per product " +
                "and kept until the dispatch pipeline drains them.")
            .ProducesValidationProblem();

        endpoints.MapGet(AggregatesPath, GetAggregatesAsync)
            .WithTags("Orders")
            .WithName("GetOrderAggregates")
            .WithSummary("Returns the currently aggregated quantity per product.");

        return endpoints;
    }

    /// <summary>
    /// Validates and aggregates a submitted batch of order lines.
    /// </summary>
    /// <param name="request">The submitted order lines.</param>
    /// <param name="aggregator">The aggregator the batch is handed to.</param>
    /// <param name="options">Aggregation configuration.</param>
    /// <param name="loggerFactory">Factory for the endpoint logger.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>A receipt for the accepted batch, or the validation errors.</returns>
    private static async Task<Results<Accepted<OrderBatchReceipt>, ValidationProblem>> SubmitOrdersAsync(
        OrderItemRequest[]? request,
        IOrderAggregator aggregator,
        IOptions<OrderAggregationOptions> options,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var validation = OrderBatchValidator.Validate(request, options.Value.MaxLinesPerRequest);

        if (!validation.IsValid)
        {
            var logger = loggerFactory.CreateLogger(LoggerCategory);
            Log.OrderBatchRejected(logger, validation.Errors.Count);

            return TypedResults.ValidationProblem(
                validation.Errors,
                detail: "One or more order lines were rejected.",
                title: "Invalid order batch");
        }

        var receipt = await aggregator.AggregateAsync(validation.Lines, cancellationToken).ConfigureAwait(false);

        return TypedResults.Accepted(AggregatesPath, receipt);
    }

    /// <summary>
    /// Returns the current aggregate.
    /// </summary>
    /// <param name="aggregator">The aggregator to read from.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The current aggregation snapshot.</returns>
    private static async Task<Ok<AggregationSnapshot>> GetAggregatesAsync(
        IOrderAggregator aggregator,
        CancellationToken cancellationToken)
    {
        var snapshot = await aggregator.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(snapshot);
    }
}
