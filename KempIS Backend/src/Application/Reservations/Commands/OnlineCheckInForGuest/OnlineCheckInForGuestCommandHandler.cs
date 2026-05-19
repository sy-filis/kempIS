using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Configuration;
using Application.Reservations.Guests;
using Domain.Reservations;
using Domain.Reservations.Guests;
using Domain.Reservations.Nationalities;
using Domain.Reservations.Reservations;
using Domain.Reservations.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Application.Reservations.Commands.OnlineCheckInForGuest;

internal sealed class OnlineCheckInForGuestCommandHandler(
  IApplicationDbContext context,
  IDateTimeProvider dateTimeProvider,
  IOptions<RetentionSettings> retentionSettings)
  : ICommandHandler<OnlineCheckInForGuestCommand>
{
  public async Task<Result> Handle(
    OnlineCheckInForGuestCommand command,
    CancellationToken cancellationToken)
  {
    Reservation? reservation = await context.Reservations
      .FirstOrDefaultAsync(r => r.Id == command.ReservationId, cancellationToken);

    if (reservation is null || !string.Equals(reservation.Secret, command.Secret, StringComparison.Ordinal))
    {
      return Result.Failure(ReservationErrors.NotFound(command.ReservationId));
    }

    Result transition = reservation.SubmitOnlineCheckIn();
    if (transition.IsFailure)
    {
      return transition;
    }

    var referencedNationalityIds = command.Guests
      .Select(x => x.NationalityId)
      .ToHashSet();

    Dictionary<Guid, Nationality> nationalitiesById = await context.Nationalities
      .Where(n => referencedNationalityIds.Contains(n.Id))
      .ToDictionaryAsync(n => n.Id, cancellationToken);

    byte[]?[] signaturesByIndex = new byte[]?[command.Guests.Count];
    DateTime now = dateTimeProvider.UtcNow;

    for (int i = 0; i < command.Guests.Count; i++)
    {
      OnlineCheckInGuest g = command.Guests[i];
      if (!nationalitiesById.TryGetValue(g.NationalityId, out Nationality? nationality))
      {
        continue;
      }

      Result documentValidation = ValidateDocumentAndVisa(i, g, nationality, reservation.Period.From);
      if (documentValidation.IsFailure)
      {
        return documentValidation;
      }

      if (GuestSignatureRules.RequiresSignature(nationality.Alpha2))
      {
        if (g.SignaturePngBase64 is null)
        {
          return Result.Failure(OnlineCheckInErrors.SignatureRequired(i));
        }
        signaturesByIndex[i] = Convert.FromBase64String(g.SignaturePngBase64);
      }
    }

    List<Guest> existingGuests = await context.Guests
      .Where(g => g.ReservationId == command.ReservationId)
      .ToListAsync(cancellationToken);
    context.Guests.RemoveRange(existingGuests);

    List<Vehicle> existingVehicles = await context.Vehicles
      .Where(v => v.ReservationId == command.ReservationId)
      .ToListAsync(cancellationToken);
    context.Vehicles.RemoveRange(existingVehicles);

    for (int i = 0; i < command.Guests.Count; i++)
    {
      OnlineCheckInGuest g = command.Guests[i];
      byte[]? signature = signaturesByIndex[i];
      context.Guests.Add(new Guest
      {
        Id = Guid.NewGuid(),
        ReservationId = command.ReservationId,
        FirstName = g.FirstName,
        LastName = g.LastName,
        DateOfBirth = g.BirthDate,
        NationalityId = g.NationalityId,
        DocumentType = g.DocumentType,
        DocumentNumber = string.IsNullOrEmpty(g.DocumentNumber) ? null : g.DocumentNumber,
        VisaNumber = string.IsNullOrEmpty(g.VisaNumber) ? null : g.VisaNumber,
        Address = g.Address,
        ReasonOfStay = "Tourism",
        StayDateRange = reservation.Period,
        Scartation = reservation.Period.To.AddYears(retentionSettings.Value.GuestYears),
        SignaturePng = signature,
        SignatureCapturedAtUtc = signature is null ? null : now,
      });
    }

    foreach (OnlineCheckInVehicle v in command.Vehicles)
    {
      context.Vehicles.Add(new Vehicle
      {
        Id = Guid.NewGuid(),
        ReservationId = command.ReservationId,
        RegistrationNumber = v.RegistrationNumber,
        BillId = null,
        ServiceId = null,
      });
    }

    await context.SaveChangesAsync(cancellationToken);
    return Result.Success();
  }

  private static Result ValidateDocumentAndVisa(
    int index,
    OnlineCheckInGuest guest,
    Nationality nationality,
    DateOnly stayStart)
  {
    bool isCzech = nationality.Alpha2 == "CZ";
    bool isCzechMinor = isCzech && AgeAt(guest.BirthDate, stayStart) < 15;
    bool documentNumberProvided = !string.IsNullOrEmpty(guest.DocumentNumber);

    if (guest.DocumentType is null)
    {
      if (!isCzechMinor || documentNumberProvided)
      {
        return Result.Failure(OnlineCheckInErrors.DocumentTypeRequired(index));
      }
      return Result.Success();
    }

    if (!documentNumberProvided)
    {
      return Result.Failure(OnlineCheckInErrors.DocumentNumberRequired(index));
    }

    DocumentType type = guest.DocumentType.Value;
    bool typeAllowed = (isCzech, nationality.IsEu) switch
    {
      (true, _) => type is DocumentType.Passport or DocumentType.IdCard,
      (false, true) => type is DocumentType.Passport or DocumentType.IdCard,
      (false, false) => type != DocumentType.IdCard,
    };
    if (!typeAllowed)
    {
      return Result.Failure(OnlineCheckInErrors.DocumentTypeNotAllowed(index, type, nationality.Alpha2));
    }

    if (type == DocumentType.Passport && nationality.VisaRequired)
    {
      if (string.IsNullOrEmpty(guest.VisaNumber))
      {
        return Result.Failure(OnlineCheckInErrors.VisaNumberRequired(index));
      }
      if (string.Equals(guest.VisaNumber, "BIOMETRIKA", StringComparison.Ordinal)
        && !nationality.BiometricsRequired)
      {
        return Result.Failure(OnlineCheckInErrors.BiometrikaNotAllowed(index));
      }
    }

    return Result.Success();
  }

  private static int AgeAt(DateOnly birthDate, DateOnly date)
  {
    int age = date.Year - birthDate.Year;
    if (date < birthDate.AddYears(age))
    {
      age--;
    }
    return age;
  }
}
