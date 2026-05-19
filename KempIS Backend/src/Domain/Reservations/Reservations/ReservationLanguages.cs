namespace Domain.Reservations;

public static class ReservationLanguages
{
  public const string Czech = "cs";
  public const string English = "en";

  public static readonly IReadOnlySet<string> All =
    new HashSet<string>(StringComparer.Ordinal) { Czech, English };
}
