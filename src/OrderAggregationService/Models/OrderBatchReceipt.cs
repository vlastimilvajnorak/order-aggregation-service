namespace OrderAggregationService.Models;

/// <summary>
/// Confirmation returned to the client once a batch of orders has been accepted.
/// </summary>
/// <param name="BatchId">Identifier assigned to the accepted batch.</param>
/// <param name="AcceptedOrderCount">Number of orders accepted in the batch.</param>
/// <param name="AcceptedQuantity">Sum of the quantities accepted in the batch.</param>
/// <param name="ReceivedAtUtc">Timestamp at which the batch was accepted.</param>
public sealed record OrderBatchReceipt(
    Guid BatchId,
    int AcceptedOrderCount,
    long AcceptedQuantity,
    DateTimeOffset ReceivedAtUtc);
