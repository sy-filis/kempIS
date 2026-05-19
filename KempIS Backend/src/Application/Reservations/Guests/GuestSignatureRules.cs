namespace Application.Reservations.Guests;

// CheckInReservationCommandHandler and SubmitGuestsToPoliceCommandHandler inline this
// predicate as a SQL filter; keep them in sync if this rule changes.
internal static class GuestSignatureRules
{
  public static bool RequiresSignature(string nationalityAlpha2)
    => !string.Equals(nationalityAlpha2, "CZ", StringComparison.Ordinal);
}
