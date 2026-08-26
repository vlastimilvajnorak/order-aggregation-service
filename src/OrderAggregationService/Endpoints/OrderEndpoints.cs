using Microsoft.AspNetCore.Http.HttpResults;

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
            .WithSummary("Submits a batch of orders for aggregation.")
            .WithDescription(
                "Accepts a JSON array of orders. Quantities are accumulated per product " +
                "and kept until the dispatch pipeline drains them.")
            .Accepts<OrderRequest[]>("application/json")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithMetadata(new ResponseDescription(
                StatusCodes.Status202Accepted,
                "The batch was accepted and its quantities were added to the aggregate. The body " +
                "is a receipt and the Location header points at the aggregate resource."))
            .WithMetadata(new ResponseDescription(
                StatusCodes.Status400BadRequest,
                "The batch was rejected as a whole and nothing was aggregated. The problem details " +
                "carry one entry per offending member, keyed 'request' for a whole-batch problem or " +
                "'[index].productId' and '[index].quantity' for a single order. A body the " +
                "server could not parse at all, or one over the request size limit, is also " +
                "reported here, with the reason in the problem detail."))
            .WithMetadata(new ResponseDescription(
                StatusCodes.Status415UnsupportedMediaType,
                "The request did not declare Content-Type: application/json. Nothing was read " +
                "and nothing was aggregated."))
            .WithMetadata(new ResponseDescription(
                StatusCodes.Status500InternalServerError,
                "The batch could not be processed because of a fault inside the service. Whether " +
                "any of it was aggregated is undefined; the traceId correlates the response with " +
                "the server logs."));

        endpoints.MapGet(AggregatesPath, GetAggregatesAsync)
            .WithTags("Orders")
            .WithName("GetOrderAggregates")
            .WithSummary("Returns the currently aggregated quantity per product.")
            .WithDescription(
                "Reports what has accumulated since the last hand-over to the downstream " +
                "system, together with the lifetime counters of accepted requests and orders. " +
                "Products are ordered by identifier.")
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithMetadata(new ResponseDescription(
                StatusCodes.Status200OK,
                "The aggregate at the moment of the call. An empty items array means nothing has " +
                "been submitted since the last hand-over, not that the service is idle."))
            .WithMetadata(new ResponseDescription(
                StatusCodes.Status500InternalServerError,
                "The aggregate could not be read because of a fault inside the service. The " +
                "traceId correlates the response with the server logs."));

        return endpoints;
    }

    /// <summary>
    /// Validates and aggregates a submitted batch of orders.
    /// </summary>
    /// <param name="request">The submitted orders.</param>
    /// <param name="aggregator">The aggregator the batch is handed to.</param>
    /// <param name="options">Aggregation configuration.</param>
    /// <param name="loggerFactory">Factory for the endpoint logger.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>A receipt for the accepted batch, or the validation errors.</returns>
    /// <response code="202">
    /// The batch was accepted and its quantities were added to the aggregate. The body is a
    /// receipt, and the Location header points at the aggregate resource.
    /// </response>
    /// <response code="400">
    /// The batch was rejected as a whole and nothing was aggregated. The problem details
    /// carry one entry per offending member, keyed as <c>request</c> for a whole-batch
    /// problem or <c>[index].productId</c> and <c>[index].quantity</c> for a single order.
    /// A body that could not be parsed is reported here too.
    /// </response>
    /// <response code="415">The request did not declare <c>Content-Type: application/json</c>.</response>
    /// <response code="500">The batch could not be processed because of a fault inside the service.</response>
    private static async Task<Results<Accepted<OrderBatchReceipt>, ValidationProblem>> SubmitOrdersAsync(
        OrderRequest[]? request,
        IOrderAggregator aggregator,
        IOptions<OrderAggregationOptions> options,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var validation = OrderBatchValidator.Validate(
            request,
            options.Value.MaxOrdersPerRequest,
            options.Value.MaxProductIdLength);

        if (!validation.IsValid)
        {
            var logger = loggerFactory.CreateLogger(LoggerCategory);
            Log.OrderBatchRejected(logger, validation.Errors.Count);

            return TypedResults.ValidationProblem(
                validation.Errors,
                detail: "One or more orders were rejected.",
                title: "Invalid order batch");
        }

        var receipt = await aggregator.AggregateAsync(validation.Orders, cancellationToken).ConfigureAwait(false);

        return TypedResults.Accepted(AggregatesPath, receipt);
    }

    /// <summary>
    /// Returns the current aggregate.
    /// </summary>
    /// <param name="aggregator">The aggregator to read from.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The current aggregation snapshot.</returns>
    /// <response code="200">
    /// The aggregate at the moment of the call. An empty <c>items</c> array means nothing
    /// has been submitted since the last hand-over, not that the service is idle.
    /// </response>
    /// <response code="500">The aggregate could not be read because of a fault inside the service.</response>
    private static async Task<Ok<AggregationSnapshot>> GetAggregatesAsync(
        IOrderAggregator aggregator,
        CancellationToken cancellationToken)
    {
        var snapshot = await aggregator.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(snapshot);
    }
}
