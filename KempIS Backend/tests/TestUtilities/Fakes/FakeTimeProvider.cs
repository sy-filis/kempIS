namespace TestUtilities.Fakes;

public sealed class FakeTimeProvider : TimeProvider
{
  public static readonly DateTimeOffset DefaultUtcNow =
    new(2026, 4, 20, 12, 0, 0, TimeSpan.Zero);

  private DateTimeOffset _utcNow;

  public FakeTimeProvider() : this(DefaultUtcNow)
  {
  }

  public FakeTimeProvider(DateTimeOffset utcNow)
  {
    _utcNow = utcNow;
  }

  public override DateTimeOffset GetUtcNow() => _utcNow;

  public void SetUtcNow(DateTimeOffset utcNow) => _utcNow = utcNow;

  public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
}
