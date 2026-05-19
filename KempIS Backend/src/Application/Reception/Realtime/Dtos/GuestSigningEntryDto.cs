namespace Application.Reception.Realtime.Dtos;

public sealed record GuestSigningEntryDto(
  Guid ClientGuestId,
  string FullName,
  string Nationality,
  bool IsCzech,
  bool HasSignature,
  bool HasEDokladyResult);
