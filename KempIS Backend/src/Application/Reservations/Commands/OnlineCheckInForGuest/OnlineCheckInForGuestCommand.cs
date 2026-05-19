using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Reservations.Guests;

namespace Application.Reservations.Commands.OnlineCheckInForGuest;

public sealed record OnlineCheckInGuest(
  string FirstName,
  string LastName,
  DateOnly BirthDate,
  Guid NationalityId,
  DocumentType? DocumentType,
  string? DocumentNumber,
  string? VisaNumber,
  Address Address,
  string? SignaturePngBase64);

public sealed record OnlineCheckInVehicle(
  string RegistrationNumber);

public sealed record OnlineCheckInForGuestCommand(
  Guid ReservationId,
  string Secret,
  IReadOnlyList<OnlineCheckInGuest> Guests,
  IReadOnlyList<OnlineCheckInVehicle> Vehicles) : ICommand;
