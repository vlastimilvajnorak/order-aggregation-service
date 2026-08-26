namespace OrderAggregationService.Endpoints;

/// <summary>
/// Outcome of validating a submitted order batch.
/// </summary>
public sealed class OrderBatchValidationResult
{
    private static readonly IReadOnlyList<Order> NoLines = [];
    private static readonly IDictionary<string, string[]> NoErrors =
        new Dictionary<string, string[]>(StringComparer.Ordinal);

    private OrderBatchValidationResult(IReadOnlyList<Order> orders, IDictionary<string, string[]> errors)
    {
        Orders = orders;
        Errors = errors;
    }

    /// <summary>
    /// Gets a value indicating whether the batch passed validation.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Gets the validated orders. Empty when <see cref="IsValid"/> is <see langword="false"/>.
    /// </summary>
    public IReadOnlyList<Order> Orders { get; }

    /// <summary>
    /// Gets the validation errors keyed by the offending request member, in the shape expected
    /// by <c>ValidationProblemDetails</c>.
    /// </summary>
    public IDictionary<string, string[]> Errors { get; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="orders">The validated orders.</param>
    /// <returns>A valid result.</returns>
    public static OrderBatchValidationResult Valid(IReadOnlyList<Order> orders)
    {
        ArgumentNullException.ThrowIfNull(orders);

        return new OrderBatchValidationResult(orders, NoErrors);
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="errors">The collected validation errors. Must not be empty.</param>
    /// <returns>An invalid result.</returns>
    public static OrderBatchValidationResult Invalid(IDictionary<string, string[]> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return new OrderBatchValidationResult(NoLines, errors);
    }
}
