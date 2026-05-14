namespace KempISGatesService.Infrastructure;

// 2000-01-01 seconds-since epoch used by the MDB schemas (ValidFrom/ValidTo/DateTime).
public static class LegacyTime
{
  public static readonly DateTime Epoch = new(2000, 1, 1, 0, 0, 0);

  public static int ToSeconds(DateTimeOffset value)
  {
    return checked((int)(value.LocalDateTime - Epoch).TotalSeconds);
  }

  public static bool CanRepresent(DateTimeOffset value)
  {
    double seconds = (value.LocalDateTime - Epoch).TotalSeconds;
    return seconds >= int.MinValue && seconds <= int.MaxValue;
  }
}
