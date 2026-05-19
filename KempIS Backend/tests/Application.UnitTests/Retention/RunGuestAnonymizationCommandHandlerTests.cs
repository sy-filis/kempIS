using Application.Retention;
using Domain.Common;
using Domain.Reservations.Guests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel;

namespace Application.UnitTests.Retention;

public sealed class RunGuestAnonymizationCommandHandlerTests : HandlerTestBase
{
  private static readonly Guid CountryId = Guid.NewGuid();
  private static readonly Guid NationalityId = Guid.NewGuid();
  private static readonly DateOnly Dob = new(1990, 1, 1);
  private static readonly DateRange Stay =
    new(new DateOnly(2026, 4, 20), new DateOnly(2026, 4, 22));

  private RunGuestAnonymizationCommandHandler CreateSut() =>
    new(Db, NullLogger<RunGuestAnonymizationCommandHandler>.Instance);

  private static Address Addr() => new(CountryId, "Prague", "10000", "Main", "1");

  private static Guest NewGuest(DateOnly? scartation, bool withOptionalPii = false) => new()
  {
    Id = Guid.NewGuid(),
    FirstName = "John",
    LastName = "Doe",
    NationalityId = NationalityId,
    DateOfBirth = Dob,
    DocumentType = withOptionalPii ? DocumentType.IdCard : null,
    DocumentNumber = withOptionalPii ? "D1" : null,
    Address = Addr(),
    ReasonOfStay = "Holiday",
    StayDateRange = Stay,
    VisaNumber = withOptionalPii ? "V-42" : null,
    Note = withOptionalPii ? "Allergic to nuts" : null,
    SignaturePng = withOptionalPii ? [0x89, 0x50, 0x4E, 0x47] : null,
    SignatureCapturedAtUtc = withOptionalPii ? DateTime.UtcNow : null,
    Scartation = scartation,
  };

  [Fact]
  public async Task Handle_NoDueGuests_ReturnsZeroAndLeavesRowsIntact()
  {
    var today = new DateOnly(2026, 5, 8);
    Db.Guests.Add(NewGuest(scartation: null));
    Db.Guests.Add(NewGuest(scartation: today.AddDays(1)));
    await Db.SaveChangesAsync();

    Result<int> result = await CreateSut().Handle(new RunGuestAnonymizationCommand(today), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBe(0);
    Guest held = await Db.Guests.FirstAsync(g => g.Scartation == null);
    held.FirstName.ShouldBe("John");
  }

  [Fact]
  public async Task Handle_GuestWithPastScartation_IsAnonymized()
  {
    var today = new DateOnly(2026, 5, 8);
    Guest due = NewGuest(scartation: today.AddDays(-1), withOptionalPii: true);
    Db.Guests.Add(due);
    await Db.SaveChangesAsync();

    Result<int> result = await CreateSut().Handle(new RunGuestAnonymizationCommand(today), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBe(1);
    Guest reloaded = (await Db.Guests.FindAsync(due.Id))!;
    reloaded.FirstName.ShouldBe("Anonymized");
    reloaded.LastName.ShouldBe("Anonymized");
    reloaded.Scartation.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_GuestWithTodayScartation_IsAnonymized()
  {
    var today = new DateOnly(2026, 5, 8);
    Guest due = NewGuest(scartation: today);
    Db.Guests.Add(due);
    await Db.SaveChangesAsync();

    Result<int> result = await CreateSut().Handle(new RunGuestAnonymizationCommand(today), CancellationToken.None);

    result.Value.ShouldBe(1);
    Guest reloaded = (await Db.Guests.FindAsync(due.Id))!;
    reloaded.FirstName.ShouldBe("Anonymized");
    reloaded.Scartation.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_MixedGuests_AnonymizesOnlyDue()
  {
    var today = new DateOnly(2026, 5, 8);
    Guest legalHold = NewGuest(scartation: null);
    Guest future = NewGuest(scartation: today.AddDays(1));
    Guest due = NewGuest(scartation: today.AddDays(-1));
    Db.Guests.AddRange(legalHold, future, due);
    await Db.SaveChangesAsync();

    Result<int> result = await CreateSut().Handle(new RunGuestAnonymizationCommand(today), CancellationToken.None);

    result.Value.ShouldBe(1);
    (await Db.Guests.FindAsync(legalHold.Id))!.FirstName.ShouldBe("John");
    (await Db.Guests.FindAsync(future.Id))!.FirstName.ShouldBe("John");
    (await Db.Guests.FindAsync(due.Id))!.FirstName.ShouldBe("Anonymized");
  }

  [Fact]
  public async Task Handle_AnonymizesNameAddressDocumentVisaSignatureAndNote()
  {
    var today = new DateOnly(2026, 5, 8);
    Guest due = NewGuest(scartation: today.AddDays(-1), withOptionalPii: true);
    Db.Guests.Add(due);
    await Db.SaveChangesAsync();

    await CreateSut().Handle(new RunGuestAnonymizationCommand(today), CancellationToken.None);

    Guest reloaded = (await Db.Guests.FindAsync(due.Id))!;
    reloaded.FirstName.ShouldBe("Anonymized");
    reloaded.LastName.ShouldBe("Anonymized");
    reloaded.Address.City.ShouldBe("Anonymized");
    reloaded.Address.Street.ShouldBe("Anonymized");
    reloaded.Address.ZipCode.ShouldBe("Anonymized");
    reloaded.Address.HouseNumber.ShouldBe("Anonymized");
    reloaded.DocumentType.ShouldBeNull();
    reloaded.DocumentNumber.ShouldBeNull();
    reloaded.VisaNumber.ShouldBeNull();
    reloaded.Note.ShouldBeNull();
    reloaded.SignaturePng.ShouldBeNull();
    reloaded.SignatureCapturedAtUtc.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_PreservesNationalityDobReasonOfStayAndStayWindow()
  {
    var today = new DateOnly(2026, 5, 8);
    Guest due = NewGuest(scartation: today.AddDays(-1));
    Db.Guests.Add(due);
    await Db.SaveChangesAsync();

    await CreateSut().Handle(new RunGuestAnonymizationCommand(today), CancellationToken.None);

    Guest reloaded = (await Db.Guests.FindAsync(due.Id))!;
    reloaded.NationalityId.ShouldBe(NationalityId);
    reloaded.DateOfBirth.ShouldBe(Dob);
    reloaded.ReasonOfStay.ShouldBe("Holiday");
    reloaded.StayDateRange.ShouldBe(Stay);
  }

  [Fact]
  public async Task Handle_PreservesAddressCountryId()
  {
    var today = new DateOnly(2026, 5, 8);
    Guest due = NewGuest(scartation: today.AddDays(-1));
    Db.Guests.Add(due);
    await Db.SaveChangesAsync();

    await CreateSut().Handle(new RunGuestAnonymizationCommand(today), CancellationToken.None);

    (await Db.Guests.FindAsync(due.Id))!.Address.CountryId.ShouldBe(CountryId);
  }

  [Fact]
  public async Task Handle_RerunSameDay_IsNoop()
  {
    var today = new DateOnly(2026, 5, 8);
    Db.Guests.Add(NewGuest(scartation: today.AddDays(-1)));
    await Db.SaveChangesAsync();

    Result<int> first = await CreateSut().Handle(new RunGuestAnonymizationCommand(today), CancellationToken.None);
    Result<int> second = await CreateSut().Handle(new RunGuestAnonymizationCommand(today), CancellationToken.None);

    first.Value.ShouldBe(1);
    second.Value.ShouldBe(0);
  }
}
