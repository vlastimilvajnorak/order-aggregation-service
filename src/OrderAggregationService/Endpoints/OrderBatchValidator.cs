using OrderAggregationService.Models;

namespace OrderAggregationService.Endpoints;

/// <summary>
/// Validates a submitted order batch and projects it onto domain order lines.
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
    /// <param name="maxLinesPerRequest">Maximum number of lines accepted in one request.</param>
    /// <returns>
    /// A result carrying either the validated order lines or the collected validation errors.
    /// </returns>
    public static OrderBatchValidationResult Validate(
        IReadOnlyList<OrderItemRequest>? request,
        int maxLinesPerRequest)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (request is null || request.Count == 0)
        {
            errors[BatchErrorKey] = ["The request body must contain at least one order line."];
            return OrderBatchValidationResult.Invalid(errors);
        }

        if (request.Count > maxLinesPerRequest)
        {
            errors[BatchErrorKey] =
            [
                $"A single request must not contain more than {maxLinesPerRequest} order lines.",
            ];
            return OrderBatchValidationResult.Invalid(errors);
        }

        var lines = new List<OrderLine>(request.Count);

        for (var index = 0; index < request.Count; index++)
        {
            var item = request[index];

            if (item is null)
            {
                errors[$"[{index}]"] = ["The order line must not be null."];
                continue;
            }

            var isValid = true;

            if (string.IsNullOrWhiteSpace(item.ProductId))
            {
                errors[$"[{index}].productId"] = ["productId is required and must not be empty."];
                isValid = false;
            }

            if (item.Quantity <= 0)
            {
                errors[$"[{index}].quantity"] = ["quantity must be greater than zero."];
                isValid = false;
            }

            if (isValid)
            {
                lines.Add(new OrderLine(item.ProductId!, item.Quantity));
            }
        }

        return errors.Count == 0
            ? OrderBatchValidationResult.Valid(lines)
            : OrderBatchValidationResult.Invalid(errors);
    }
}
