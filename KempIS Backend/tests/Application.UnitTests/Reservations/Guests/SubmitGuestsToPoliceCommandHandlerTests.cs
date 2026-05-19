using Application.Abstractions.Reservations;
using Application.Configuration;
using Application.Reservations.Guests.Commands.SubmitGuestsToPolice;
using Domain.Common;
using Domain.Reservations.Guests;
using Domain.Reservations.Nationalities;
using Domain.Reservations.PoliceReports;
using Infrastructure.Database;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using SharedKernel;
using TestUtilities.Fakes;

namespace Application.UnitTests.Reservations.Guests;

public sealed class SubmitGuestsToPoliceCommandHandlerTests : HandlerTestBase
{
  private readonly IPoliceGuestReporter _reporter = Substitute.For<IPoliceGuestReporter>();

  private static IOptions<CampSettings> DefaultCampSettings() =>
    Options.Create(new CampSettings
    {
      CheckOutTime = new TimeOnly(10, 0),
    });

  private SubmitGuestsToPoliceCommandHandler CreateSut() => new(Db, _reporter, Clock, DefaultCampSettings());

  private async Task<Nationality> SeedNationality(string alpha2)
  {
    Nationality n = new()
    {
      Id = Guid.NewGuid(),
      Name = alpha2,
      NameEn = "Test",
      Alpha2 = alpha2,
      Alpha3 = alpha2.PadRight(3, 'X'),
      Numeric = "000",
      VisaRequired = false,
      BiometricsRequired = false,
      IsEu = false,
      LanguageId = Guid.NewGuid(),
    };
    Db.Nationalities.Add(n);
    await Db.SaveChangesAsync();
    return n;
  }

  private static readonly DateTime DefaultCheckInAt = new(2026, 5, 1, 15, 0, 0, DateTimeKind.Utc);

  private Guest BuildGuest(
    Guid nationalityId,
    DocumentType type = DocumentType.Passport,
    string docNumber = "P1",
    DateTime? reportedAt = null,
    DateTime? checkOutAt = null,
    bool hasCheckIn = true,
    DateTime? checkInAt = null)
    => new()
    {
      Id = Guid.NewGuid(),
      ReservationId = Guid.NewGuid(),
      FirstName = "A",
      LastName = "B",
      NationalityId = nationalityId,
      DateOfBirth = new DateOnly(1990, 1, 1),
      DocumentType = type,
      DocumentNumber = docNumber,
      Address = new Address(Guid.NewGuid(), "Berlin", "10115", "Hauptstr", "1"),
      ReasonOfStay = "tourism",
      StayDateRange = new DateRange(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5)),
      ReportedAt = reportedAt,
      CheckInAt = hasCheckIn ? (checkInAt ?? DefaultCheckInAt) : null,
      CheckOutAt = checkOutAt,
    };

  [Fact]
  public async Task Handle_NoGuests_ReturnsSuccessAndDoesNotCallReporter()
  {
    Result result = await CreateSut().Handle(new SubmitGuestsToPoliceCommand(), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    await _reporter.DidNotReceiveWithAnyArgs().SubmitAsync(default!, default);
  }

  [Fact]
  public async Task Handle_OnlyCzechGuests_DoesNotCallReporter()
  {
    Nationality cz = await SeedNationality("CZ");
    Db.Guests.Add(BuildGuest(cz.Id));
    await Db.SaveChangesAsync();

    Result result = await CreateSut().Handle(new SubmitGuestsToPoliceCommand(), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    await _reporter.DidNotReceiveWithAnyArgs().SubmitAsync(default!, default);
  }

  [Fact]
  public async Task Handle_ForeignUnreportedGuest_CallsReporterAndStampsReportedAt()
  {
    Nationality de = await SeedNationality("DE");
    Guest g = BuildGuest(de.Id);
    Db.Guests.Add(g);
    await Db.SaveChangesAsync();

    DateTime now = new(2026, 4, 21, 12, 0, 0, DateTimeKind.Utc);
    Clock.Set(now);
    _reporter.SubmitAsync(default!, default).ReturnsForAnyArgs(Result.Success());

    Result result = await CreateSut().Handle(new SubmitGuestsToPoliceCommand(), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    await _reporter.Received(1).SubmitAsync(
      Arg.Is<IReadOnlyCollection<PoliceGuestEntry>>(e => e.Count == 1 && e.Single().GuestId == g.Id),
      Arg.Any<CancellationToken>());
    Guest reloaded = await Db.Guests.SingleAsync();
    reloaded.ReportedAt.ShouldBe(now);
  }

  [Fact]
  public async Task Handle_GuestReportedAndNotUpdatedSince_IsSkipped()
  {
    Nationality de = await SeedNationality("DE");
    Guest g = BuildGuest(de.Id, reportedAt: new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc));
    Db.Guests.Add(g);
    await Db.SaveChangesAsync();
    g.ReportedAt = g.UpdatedAt.AddSeconds(1);
    await Db.SaveChangesAsync();

    Result result = await CreateSut().Handle(new SubmitGuestsToPoliceCommand(), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    await _reporter.DidNotReceiveWithAnyArgs().SubmitAsync(default!, default);
  }

  [Fact]
  public async Task Handle_GuestReportedButUpdatedSince_IsResubmitted()
  {
    Nationality de = await SeedNationality("DE");
    Guest g = BuildGuest(de.Id);
    Db.Guests.Add(g);
    await Db.SaveChangesAsync();
    g.ReportedAt = g.UpdatedAt.AddDays(-1);
    await Db.SaveChangesAsync();

    _reporter.SubmitAsync(default!, default).ReturnsForAnyArgs(Result.Success());

    Result result = await CreateSut().Handle(new SubmitGuestsToPoliceCommand(), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    await _reporter.Received(1).SubmitAsync(Arg.Any<IReadOnlyCollection<PoliceGuestEntry>>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_ReporterFails_DoesNotStampReportedAtAndPropagatesError()
  {
    Nationality de = await SeedNationality("DE");
    Db.Guests.Add(BuildGuest(de.Id));
    await Db.SaveChangesAsync();

    _reporter.SubmitAsync(default!, default).ReturnsForAnyArgs(Result.Failure(PoliceReportErrors.Unavailable));

    Result result = await CreateSut().Handle(new SubmitGuestsToPoliceCommand(), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(PoliceReportErrors.Unavailable);
    Guest reloaded = await Db.Guests.SingleAsync();
    reloaded.ReportedAt.ShouldBeNull();
  }

  [Theory]
  [InlineData(DocumentType.Passport, "P1", "P1", null)]
  [InlineData(DocumentType.IdCard, "ID1", "ID1", null)]
  [InlineData(DocumentType.CzechResidencePermit, "PP123", "NONE", "POBYT PP123")]
  [InlineData(DocumentType.LostPassportConfirmation, "REF-42", "NONE", "REF-42")]
  [InlineData(DocumentType.CzechDiplomatCard, "D42", "NONE", "DIPLOMAT D42")]
  [InlineData(DocumentType.ChildInParentPassport, "PARENT1", "PARENT1", "ČÍSLO DOKLADU RODIČE")]
  public async Task Handle_MapsDocumentTypeCorrectly(
    DocumentType type, string docNumber,
    string expectedCDocN, string? expectedNote)
  {
    Nationality de = await SeedNationality("DE");
    Guest g = BuildGuest(de.Id, type: type, docNumber: docNumber);
    Db.Guests.Add(g);
    await Db.SaveChangesAsync();

    IReadOnlyCollection<PoliceGuestEntry>? captured = null;
    _reporter.SubmitAsync(default!, default)
      .ReturnsForAnyArgs(ci => { captured = ci.Arg<IReadOnlyCollection<PoliceGuestEntry>>(); return Task.FromResult(Result.Success()); });

    Result result = await CreateSut().Handle(new SubmitGuestsToPoliceCommand(), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    PoliceGuestEntry entry = captured!.Single();
    entry.DocumentNumberForCDocN.ShouldBe(expectedCDocN);
    entry.Note.ShouldBe(expectedNote);
  }

  [Fact]
  public async Task Handle_GuestWithoutCheckInAt_IsSkipped()
  {
    Nationality de = await SeedNationality("DE");
    Guest g = BuildGuest(de.Id, hasCheckIn: false);
    Db.Guests.Add(g);
    await Db.SaveChangesAsync();

    Result result = await CreateSut().Handle(new SubmitGuestsToPoliceCommand(), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    await _reporter.DidNotReceiveWithAnyArgs().SubmitAsync(default!, default);
  }

  [Fact]
  public async Task Handle_UsesCheckOutAtWhenPresent()
  {
    Nationality de = await SeedNationality("DE");
    var checkOut = new DateTime(2026, 5, 5, 11, 30, 0, DateTimeKind.Utc);
    Guest g = BuildGuest(de.Id, checkOutAt: checkOut);
    Db.Guests.Add(g);
    await Db.SaveChangesAsync();

    IReadOnlyCollection<PoliceGuestEntry>? captured = null;
    _reporter.SubmitAsync(default!, default)
      .ReturnsForAnyArgs(ci => { captured = ci.Arg<IReadOnlyCollection<PoliceGuestEntry>>(); return Task.FromResult(Result.Success()); });

    Result result = await CreateSut().Handle(new SubmitGuestsToPoliceCommand(), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    captured!.Single().StayUntil.ShouldBe(checkOut);
  }

  [Fact]
  public async Task Handle_UsesStayDateRangeEndWithCampCheckOutTimeWhenCheckOutAtNull()
  {
    Nationality de = await SeedNationality("DE");
    // StayDateRange.To = 2026-05-05, CheckOutAt = null, CampSettings.CheckOutTime = 10:00
    // DateOnly.ToDateTime(TimeOnly) returns Kind=Unspecified
    Guest g = BuildGuest(de.Id, checkOutAt: null);
    Db.Guests.Add(g);
    await Db.SaveChangesAsync();

    IReadOnlyCollection<PoliceGuestEntry>? captured = null;
    _reporter.SubmitAsync(default!, default)
      .ReturnsForAnyArgs(ci => { captured = ci.Arg<IReadOnlyCollection<PoliceGuestEntry>>(); return Task.FromResult(Result.Success()); });

    Result result = await CreateSut().Handle(new SubmitGuestsToPoliceCommand(), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    // DateOnly(2026,5,5).ToDateTime(new TimeOnly(10,0)) => Kind=Unspecified
    captured!.Single().StayUntil.ShouldBe(new DateTime(2026, 5, 5, 10, 0, 0, DateTimeKind.Unspecified));
  }

  [Fact]
  public async Task Handle_ResubmitCorrection_IncludesCorrectionMarkerInNote()
  {
    Nationality de = await SeedNationality("DE");
    // Passport -> documentTypeNote = null; correction marker should stand alone
    Guest g = BuildGuest(de.Id, type: DocumentType.Passport);
    Db.Guests.Add(g);
    await Db.SaveChangesAsync();
    // Set ReportedAt before UpdatedAt to trigger correction path
    g.ReportedAt = g.UpdatedAt.AddDays(-1);
    await Db.SaveChangesAsync();

    IReadOnlyCollection<PoliceGuestEntry>? captured = null;
    _reporter.SubmitAsync(default!, default)
      .ReturnsForAnyArgs(ci => { captured = ci.Arg<IReadOnlyCollection<PoliceGuestEntry>>(); return Task.FromResult(Result.Success()); });

    Result result = await CreateSut().Handle(new SubmitGuestsToPoliceCommand(), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    captured!.Single().Note.ShouldBe("PŘEDCHOZÍ OZNÁMENÍ OBSAHOVALO CHYBY");
  }

  [Fact]
  public async Task Handle_ResubmitCorrection_CzechResidencePermit_CombinesNotes()
  {
    Nationality de = await SeedNationality("DE");
    // CzechResidencePermit -> documentTypeNote = "POBYT PP123"
    Guest g = BuildGuest(de.Id, type: DocumentType.CzechResidencePermit, docNumber: "PP123");
    Db.Guests.Add(g);
    await Db.SaveChangesAsync();
    g.ReportedAt = g.UpdatedAt.AddDays(-1);
    await Db.SaveChangesAsync();

    IReadOnlyCollection<PoliceGuestEntry>? captured = null;
    _reporter.SubmitAsync(default!, default)
      .ReturnsForAnyArgs(ci => { captured = ci.Arg<IReadOnlyCollection<PoliceGuestEntry>>(); return Task.FromResult(Result.Success()); });

    Result result = await CreateSut().Handle(new SubmitGuestsToPoliceCommand(), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    captured!.Single().Note.ShouldBe("POBYT PP123; PŘEDCHOZÍ OZNÁMENÍ OBSAHOVALO CHYBY");
  }
}
