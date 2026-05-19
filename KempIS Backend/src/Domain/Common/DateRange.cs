namespace Domain.Common;

public sealed record DateRange
{
  public DateOnly From { get; init; }
  public DateOnly To { get; init; }

  public DateRange(DateOnly from, DateOnly to)
  {
    if (from > to)
    {
      throw new ArgumentException(
        $"'To' date ({to:yyyy-MM-dd}) must be on or after 'From' date ({from:yyyy-MM-dd}).",
        nameof(to));
    }

    From = from;
    To = to;
  }

  public bool Overlaps(DateRange other)
  {
    ArgumentNullException.ThrowIfNull(other);
    return From <= other.To && To >= other.From;
  }

  public bool Contains(DateOnly date) => From <= date && date <= To;
}
