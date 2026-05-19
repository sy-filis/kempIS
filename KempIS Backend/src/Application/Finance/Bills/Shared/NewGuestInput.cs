using Domain.Common;
using Domain.Reservations.Guests;

namespace Application.Finance.Bills.Shared;

public sealed record NewGuestInput(
  string FirstName,
  string LastName,
  Guid NationalityId,
  DateOnly DateOfBirth,
  DocumentType DocumentType,
  string DocumentNumber,
  Address Address,
  string ReasonOfStay,
  DateOnly StayFrom,
  DateOnly StayTo,
  string? VisaNumber,
  string? Note,
  bool PaysRecreationFee);
