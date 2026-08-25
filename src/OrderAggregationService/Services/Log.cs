namespace OrderAggregationService.Services;

/// <summary>
/// Compile-time generated, structured log messages used across the service.
/// </summary>
/// <remarks>
/// Using the <see cref="LoggerMessageAttribute"/> source generator keeps every message
/// strongly typed, allocation free on disabled log levels, and guarantees that the named
/// placeholders reach structured sinks as separate fields instead of a formatted string.
/// </remarks>
internal static partial class Log
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Accepted order batch {BatchId} with {AcceptedLineCount} lines and {AcceptedQuantity} units.")]
    public static partial void OrderBatchAccepted(
        ILogger logger,
        Guid batchId,
        int acceptedLineCount,
        long acceptedQuantity);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Rejected an order batch with {ErrorCount} validation errors.")]
    public static partial void OrderBatchRejected(ILogger logger, int errorCount);

    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Information,
        Message = "Periodic order dispatch is disabled by configuration; the background service is idle.")]
    public static partial void DispatchDisabled(ILogger logger);

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Information,
        Message = "Periodic order dispatch started with an interval of {DispatchInterval}.")]
    public static partial void DispatchStarted(ILogger logger, TimeSpan dispatchInterval);

    [LoggerMessage(
        EventId = 1102,
        Level = LogLevel.Debug,
        Message = "Dispatch cycle {CycleNumber} found no pending aggregates.")]
    public static partial void DispatchCycleEmpty(ILogger logger, long cycleNumber);

    [LoggerMessage(
        EventId = 1103,
        Level = LogLevel.Information,
        Message = "Dispatch cycle {CycleNumber} handed over {ProductCount} products and {TotalQuantity} units.")]
    public static partial void DispatchCycleCompleted(
        ILogger logger,
        long cycleNumber,
        int productCount,
        long totalQuantity);

    [LoggerMessage(
        EventId = 1104,
        Level = LogLevel.Error,
        Message = "Dispatch cycle {CycleNumber} failed; the aggregates it drained were lost.")]
    public static partial void DispatchCycleFailed(ILogger logger, long cycleNumber, Exception exception);

    [LoggerMessage(
        EventId = 1200,
        Level = LogLevel.Information,
        Message = "Dispatching {ProductCount} aggregated products totalling {TotalQuantity} units.")]
    public static partial void AggregatesDispatched(ILogger logger, int productCount, long totalQuantity);
}
