using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Reservations;
using Application.Configuration;
using Domain.Reservations.Guests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Application.Reservations.Guests.Commands.SubmitGuestsToPolice;

internal sealed class SubmitGuestsToPoliceCommandHandler(
  IApplicationDbContext context,
  IPoliceGuestReporter reporter,
  IDateTimeProvider dateTimeProvider,
  IOptions<CampSettings> campSettings)
  : ICommandHandler<SubmitGuestsToPoliceCommand>
{
  public async Task<Result> Handle(SubmitGuestsToPoliceCommand command, CancellationToken cancellationToken)
  {
    TimeOnly defaultCheckOut = campSettings.Value.CheckOutTime;

    List<Guest> unreported = await context.Guests
      .Include(g => g.Nationality)
      .Where(g => g.Nationality!.Alpha2 != "CZ")
      .Where(g => g.CheckInAt != null)
      .Where(g => g.ReportedAt == null || g.UpdatedAt > g.ReportedAt)
      .ToListAsync(cancellationToken);

    if (unreported.Count == 0)
    {
      return Result.Success();
    }

    var entries = unreported.Select(g => MapToEntry(g, defaultCheckOut)).ToList();

    Result submission = await reporter.SubmitAsync(entries, cancellationToken);
    if (submission.IsFailure)
    {
      return submission;
    }

    DateTime now = dateTimeProvider.UtcNow;
    foreach (Guest guest in unreported)
    {
      guest.ReportedAt = now;
    }
    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }

  private static PoliceGuestEntry MapToEntry(Guest g, TimeOnly defaultCheckOut)
  {
    (string cDocN, string? documentTypeNote) = MapDocument(g);

    string? noteWithCorrection = documentTypeNote;
    if (g.ReportedAt is not null && g.UpdatedAt > g.ReportedAt)
    {
      noteWithCorrection = string.IsNullOrEmpty(noteWithCorrection)
        ? "PŘEDCHOZÍ OZNÁMENÍ OBSAHOVALO CHYBY"
        : $"{noteWithCorrection}; PŘEDCHOZÍ OZNÁMENÍ OBSAHOVALO CHYBY";
    }

    DateTime stayUntil = g.CheckOutAt
      ?? g.StayDateRange?.To.ToDateTime(defaultCheckOut)
      ?? g.CheckInAt!.Value;

    return new PoliceGuestEntry(
      GuestId: g.Id,
      StayFrom: g.CheckInAt!.Value,
      StayUntil: stayUntil,
      LastName: g.LastName,
      FirstName: g.FirstName,
      DateOfBirth: g.DateOfBirth,
      NationalityCode: g.Nationality!.Alpha2,
      DocumentNumberForCDocN: cDocN,
      VisaNumber: g.VisaNumber,
      PermanentAddressAbroad: BuildPermanentAddress(g),
      Note: noteWithCorrection,
      PurposeOfStay: string.IsNullOrWhiteSpace(g.ReasonOfStay) ? null : g.ReasonOfStay);
  }

  private static (string CDocN, string? Note) MapDocument(Guest g) => g.DocumentType switch
  {
    DocumentType.Passport => (g.DocumentNumber!, null),
    DocumentType.IdCard => (g.DocumentNumber!, null),
    DocumentType.CzechResidencePermit => ("NONE", $"POBYT {g.DocumentNumber}"),
    DocumentType.LostPassportConfirmation => ("NONE", g.DocumentNumber),
    DocumentType.CzechDiplomatCard => ("NONE", $"DIPLOMAT {g.DocumentNumber}"),
    DocumentType.ChildInParentPassport => (g.DocumentNumber!, "ČÍSLO DOKLADU RODIČE"),
    null => throw new InvalidOperationException("Guest has no document type - should be filtered upstream."),
    _ => throw new InvalidOperationException($"Unknown document type: {g.DocumentType}")
  };

  private static string? BuildPermanentAddress(Guest g)
  {
    string joined = string.Join(", ", new[] { g.Address.City, g.Address.Street + " " + g.Address.HouseNumber }
      .Where(s => !string.IsNullOrWhiteSpace(s)));
    return string.IsNullOrWhiteSpace(joined) ? null : joined;
  }
}
