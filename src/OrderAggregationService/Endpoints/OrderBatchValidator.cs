namespace OrderAggregationService.Endpoints;

/// <summary>
/// Validates a submitted order batch and projects it onto domain orders.
/// </summary>
/// <remarks>
/// Implemented as a pure function so the rules can be unit tested without an HTTP context.
/// </remarks>
public static class OrderBatchValidator
{
    private const string BatchErrorKey = "request";

    /// <summary>
    /// Validates the submitted batch.
    /// </summary>
    /// <param name="request">The deserialized request body, which may be null or empty.</param>
    /// <param name="maxOrdersPerRequest">Maximum number of orders accepted in one request.</param>
    /// <param name="maxProductIdLength">Maximum length of a product identifier.</param>
    /// <returns>
    /// A result carrying either the validated orders or the collected validation errors.
    /// </returns>
    public static OrderBatchValidationResult Validate(
        IReadOnlyList<OrderRequest>? request,
        int maxOrdersPerRequest,
        int maxProductIdLength)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (request is null || request.Count == 0)
        {
            errors[BatchErrorKey] = ["The request body must contain at least one order."];
            return OrderBatchValidationResult.Invalid(errors);
        }

        if (request.Count > maxOrdersPerRequest)
        {
            errors[BatchErrorKey] =
            [
                $"A single request must not contain more than {maxOrdersPerRequest} orders.",
            ];
            return OrderBatchValidationResult.Invalid(errors);
        }

        var orders = new List<Order>(request.Count);

        for (var index = 0; index < request.Count; index++)
        {
            var item = request[index];

            if (item is null)
            {
                errors[$"[{index}]"] = ["The order must not be null."];
                continue;
            }

            var isValid = true;

            if (string.IsNullOrWhiteSpace(item.ProductId))
            {
                errors[$"[{index}].productId"] = ["productId is required and must not be empty."];
                isValid = false;
            }
            else if (item.ProductId.Length > maxProductIdLength)
            {
                errors[$"[{index}].productId"] =
                [
                    $"productId must not be longer than {maxProductIdLength} characters.",
                ];
                isValid = false;
            }

            if (item.Quantity <= 0)
            {
                errors[$"[{index}].quantity"] = ["quantity must be greater than zero."];
                isValid = false;
            }

            if (isValid)
            {
                orders.Add(new Order(item.ProductId!, item.Quantity));
            }
        }

        return errors.Count == 0
            ? OrderBatchValidationResult.Valid(orders)
            : OrderBatchValidationResult.Invalid(errors);
    }
}
