using Infrastructure.Retention;

namespace Infrastructure.UnitTests.Retention;

public sealed class RetentionSchedulerTests
{
  private static readonly TimeZoneInfo Prague = TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague");
  private static readonly TimeOnly Target = new(3, 0);

  private static DateTime LocalToUtc(DateTime localUnspecified) =>
    TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localUnspecified, DateTimeKind.Unspecified), Prague);

  [Fact]
  public void ComputeDelayUntilNextRun_NowBeforeTargetToday_ReturnsSameDayDelay()
  {
    DateTime nowUtc = LocalToUtc(new DateTime(2026, 5, 8, 1, 0, 0, DateTimeKind.Unspecified));

    TimeSpan delay = RetentionScheduler.ComputeDelayUntilNextRun(nowUtc, Target, Prague);

    delay.ShouldBe(TimeSpan.FromHours(2));
  }

  [Fact]
  public void ComputeDelayUntilNextRun_NowAfterTargetToday_ReturnsTomorrowDelay()
  {
    DateTime nowUtc = LocalToUtc(new DateTime(2026, 5, 8, 4, 0, 0, DateTimeKind.Unspecified));

    TimeSpan delay = RetentionScheduler.ComputeDelayUntilNextRun(nowUtc, Target, Prague);

    delay.ShouldBe(TimeSpan.FromHours(23));
  }

  [Fact]
  public void ComputeDelayUntilNextRun_NowEqualsTarget_ReturnsTomorrowDelay()
  {
    DateTime nowUtc = LocalToUtc(new DateTime(2026, 5, 8, 3, 0, 0, DateTimeKind.Unspecified));

    TimeSpan delay = RetentionScheduler.ComputeDelayUntilNextRun(nowUtc, Target, Prague);

    delay.ShouldBe(TimeSpan.FromHours(24));
  }

  [Fact]
  public void ComputeDelayUntilNextRun_AcrossDstSpringForward_StillReachesTarget()
  {
    DateTime nowUtc = LocalToUtc(new DateTime(2026, 3, 29, 1, 0, 0, DateTimeKind.Unspecified));

    TimeSpan delay = RetentionScheduler.ComputeDelayUntilNextRun(nowUtc, Target, Prague);

    delay.ShouldBeGreaterThan(TimeSpan.Zero);
    delay.ShouldBeLessThan(TimeSpan.FromHours(25));
  }

  [Fact]
  public void ComputeDelayUntilNextRun_AcrossDstFallBack_StillReachesTarget()
  {
    DateTime nowUtc = LocalToUtc(new DateTime(2026, 10, 25, 1, 0, 0, DateTimeKind.Unspecified));

    TimeSpan delay = RetentionScheduler.ComputeDelayUntilNextRun(nowUtc, Target, Prague);

    delay.ShouldBeGreaterThan(TimeSpan.Zero);
    delay.ShouldBeLessThan(TimeSpan.FromHours(25));
  }
}
