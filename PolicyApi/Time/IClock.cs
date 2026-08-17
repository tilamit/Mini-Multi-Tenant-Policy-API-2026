namespace PolicyApi.Time;

/// <summary>
/// Abstraction over "now" so the expiring-policies window is deterministic and tests are not
/// time-dependent (they cannot go flaky when run near midnight). UTC is used for a stable boundary.
/// </summary>
public interface IClock
{
    DateOnly UtcToday { get; }
}

public sealed class SystemClock : IClock
{
    public DateOnly UtcToday => DateOnly.FromDateTime(DateTime.UtcNow);
}
