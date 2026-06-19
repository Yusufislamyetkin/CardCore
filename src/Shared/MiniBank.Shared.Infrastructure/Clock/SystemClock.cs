namespace MiniBank.Shared.Infrastructure.Clock;

public sealed class SystemClock : IClock { public DateTime UtcNow => DateTime.UtcNow; }
