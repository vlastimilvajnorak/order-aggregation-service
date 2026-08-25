namespace OrderAggregationService.Tests;

/// <summary>
/// Deterministic <see cref="TimeProvider"/> so timestamp assertions do not depend on the wall clock.
/// </summary>
internal sealed class FixedTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
}
