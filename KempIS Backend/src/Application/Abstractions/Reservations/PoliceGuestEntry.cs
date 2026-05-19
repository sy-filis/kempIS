namespace Application.Abstractions.Reservations;

public sealed record PoliceGuestEntry(
  Guid GuestId,
  DateTime StayFrom,
  DateTime StayUntil,
  string LastName,
  string FirstName,
  DateOnly DateOfBirth,
  string NationalityCode,
  string DocumentNumberForCDocN,
  string? VisaNumber,
  string? PermanentAddressAbroad,
  string? Note,
  string? PurposeOfStay);
