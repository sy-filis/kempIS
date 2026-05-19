using System.Globalization;
using Domain.Common;

namespace Domain.UnitTests.Common;

public sealed class DateRangeTests
{
  private static DateOnly D(string iso) => DateOnly.Parse(iso, CultureInfo.InvariantCulture);

  [Fact]
  public void Ctor_FromEqualsTo_AllowsSingleDayRange()
  {
    var d = new DateOnly(2026, 5, 1);
    var range = new DateRange(d, d);
    range.From.ShouldBe(d);
    range.To.ShouldBe(d);
  }

  [Fact]
  public void Ctor_FromAfterTo_Throws()
  {
    var from = new DateOnly(2026, 5, 5);
    var to = new DateOnly(2026, 5, 1);
    Should.Throw<ArgumentException>(() => new DateRange(from, to))
      .ParamName.ShouldBe("to");
  }

  [Theory]
  // Identical ranges
  [InlineData("2026-05-01", "2026-05-03", "2026-05-01", "2026-05-03", true)]
  // Disjoint before
  [InlineData("2026-05-01", "2026-05-03", "2026-05-05", "2026-05-07", false)]
  // Disjoint after
  [InlineData("2026-05-05", "2026-05-07", "2026-05-01", "2026-05-03", false)]
  // Touching at one day - inclusive semantics: overlap
  [InlineData("2026-05-01", "2026-05-03", "2026-05-03", "2026-05-05", true)]
  [InlineData("2026-05-03", "2026-05-05", "2026-05-01", "2026-05-03", true)]
  // Adjacent, not touching - no overlap
  [InlineData("2026-05-01", "2026-05-02", "2026-05-03", "2026-05-04", false)]
  // Fully contained
  [InlineData("2026-05-01", "2026-05-10", "2026-05-03", "2026-05-05", true)]
  // Single-day inside other
  [InlineData("2026-05-05", "2026-05-05", "2026-05-01", "2026-05-10", true)]
  // Partial overlap
  [InlineData("2026-05-01", "2026-05-05", "2026-05-04", "2026-05-08", true)]
  public void Overlaps_ReturnsExpected(string aFrom, string aTo, string bFrom, string bTo, bool expected)
  {
    var a = new DateRange(D(aFrom), D(aTo));
    var b = new DateRange(D(bFrom), D(bTo));
    a.Overlaps(b).ShouldBe(expected);
  }

  [Fact]
  public void Overlaps_IsCommutative()
  {
    var a = new DateRange(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 10));
    var b = new DateRange(new DateOnly(2026, 5, 5), new DateOnly(2026, 5, 15));
    a.Overlaps(b).ShouldBe(b.Overlaps(a));
  }

  [Fact]
  public void Overlaps_NullOther_Throws()
  {
    var a = new DateRange(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 3));
    Should.Throw<ArgumentNullException>(() => a.Overlaps(null!));
  }

  [Theory]
  [InlineData("2026-05-01", "2026-05-05", "2026-05-03", true)]  // inside
  [InlineData("2026-05-01", "2026-05-05", "2026-05-01", true)]  // on From
  [InlineData("2026-05-01", "2026-05-05", "2026-05-05", true)]  // on To
  [InlineData("2026-05-01", "2026-05-05", "2026-04-30", false)] // before
  [InlineData("2026-05-01", "2026-05-05", "2026-05-06", false)] // after
  public void Contains_ReturnsExpected(string from, string to, string date, bool expected)
  {
    var range = new DateRange(D(from), D(to));
    range.Contains(D(date)).ShouldBe(expected);
  }

  [Fact]
  public void Contains_SingleDayRange_OnlyThatDate()
  {
    var d = new DateOnly(2026, 5, 5);
    var range = new DateRange(d, d);
    range.Contains(d).ShouldBeTrue();
    range.Contains(d.AddDays(-1)).ShouldBeFalse();
    range.Contains(d.AddDays(1)).ShouldBeFalse();
  }

  [Fact]
  public void Equality_ByValue()
  {
    var a = new DateRange(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 3));
    var b = new DateRange(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 3));
    a.ShouldBe(b);
    a.GetHashCode().ShouldBe(b.GetHashCode());
  }
}
