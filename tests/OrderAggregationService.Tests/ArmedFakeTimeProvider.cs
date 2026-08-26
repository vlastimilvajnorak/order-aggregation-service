using Microsoft.Extensions.Time.Testing;

namespace OrderAggregationService.Tests;

/// <summary>
/// A <see cref="FakeTimeProvider"/> that reports when a timer has been created against it.
/// </summary>
/// <remarks>
/// <see cref="Microsoft.Extensions.Hosting.BackgroundService.StartAsync"/> does not
/// guarantee that the service body has reached its timer by the time it returns.
/// Advancing the clock before that happens moves the due time along with it, the tick
/// never arrives, and the test hangs. Waiting for the timer first removes the race.
/// </remarks>
internal sealed class ArmedFakeTimeProvider : FakeTimeProvider, IDisposable
{
    private readonly ManualResetEventSlim _armed = new(initialState: false);

    public ArmedFakeTimeProvider(DateTimeOffset startDateTime)
        : base(startDateTime)
    {
    }

    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period)
    {
        var timer = base.CreateTimer(callback, state, dueTime, period);

        _armed.Set();

        return timer;
    }

    /// <summary>
    /// Blocks until a timer has been created, so the clock can be advanced safely.
    /// </summary>
    /// <param name="timeout">How long to wait before declaring the service broken.</param>
    public void WaitUntilArmed(TimeSpan timeout)
    {
        if (!_armed.Wait(timeout))
        {
            throw new TimeoutException(
                $"No timer was created against the clock within {timeout}.");
        }
    }

    public void Dispose() => _armed.Dispose();
}
