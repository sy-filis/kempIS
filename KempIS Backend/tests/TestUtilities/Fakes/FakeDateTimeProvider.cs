using SharedKernel;

namespace TestUtilities.Fakes;

public sealed class FakeDateTimeProvider(DateTime initialUtc) : IDateTimeProvider
{
  private DateTime _now = DateTime.SpecifyKind(initialUtc, DateTimeKind.Utc);

  public static readonly DateTime DefaultUtc = new(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc);

  public FakeDateTimeProvider() : this(DefaultUtc)
  {
  }

  public DateTime UtcNow => _now;

  public void Advance(TimeSpan by) => _now = _now.Add(by);

  public void Set(DateTime utc) => _now = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
}
