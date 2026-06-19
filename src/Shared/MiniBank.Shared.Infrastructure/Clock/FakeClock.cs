namespace MiniBank.Shared.Infrastructure.Clock;

public sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = DateTime.UtcNow;
    public void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);
    public void SetTo(DateTime dateTime) => UtcNow = dateTime;
}
