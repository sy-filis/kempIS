using Application.Configuration;
using Application.Reservations.Guests;
using Domain.Common;
using Domain.Reservations.Guests;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Application.UnitTests.Reservations.Guests;

public sealed class CreateGuestRetentionTests : HandlerTestBase
{
  private readonly RetentionSettings _retention = new()
  {
    GuestYears = 6,
    BillYears = 10,
    InvoiceYears = 10,
    RunAtLocalTime = new TimeOnly(3, 0),
  };

  private CreateGuestCommandHandler CreateSut() =>
    new(Db, Clock, Options.Create(_retention));

  private static Address Addr() => new(Guid.NewGuid(), "Prague", "10000", "Main", "1");

  private static CreateGuestCommand MakeCommand(DateOnly? scartation, DateRange? stay) => new(
    ReservationId: Guid.NewGuid(),
    BillId: null,
    PaysRecreationFee: null,
    FirstName: "John",
    LastName: "Doe",
    NationalityId: Guid.NewGuid(),
    DateOfBirth: new DateOnly(1990, 1, 1),
    DocumentType: DocumentType.IdCard,
    DocumentNumber: "D1",
    Address: Addr(),
    ReasonOfStay: "Holiday",
    StayDateRange: stay,
    VisaNumber: null,
    Note: null,
    Scartation: scartation,
    CheckInAt: null,
    CheckOutAt: null,
    SignaturePngBase64: null);

  [Fact]
  public async Task Handle_NoScartationProvided_DefaultsFromConfig()
  {
    var stay = new DateRange(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 5));
    Result<Guid> result = await CreateSut()
      .Handle(MakeCommand(scartation: null, stay), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Guest stored = (await Db.Guests.FindAsync(result.Value))!;
    stored.Scartation.ShouldBe(stay.To.AddYears(6));
  }

  [Fact]
  public async Task Handle_ExplicitScartation_OverridesDefault()
  {
    var stay = new DateRange(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 5));
    var explicitDate = new DateOnly(2030, 1, 1);
    Result<Guid> result = await CreateSut()
      .Handle(MakeCommand(scartation: explicitDate, stay), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Guest stored = (await Db.Guests.FindAsync(result.Value))!;
    stored.Scartation.ShouldBe(explicitDate);
  }

  [Fact]
  public async Task Handle_NoScartationAndNoStayDateRange_LeavesScartationNull()
  {
    Result<Guid> result = await CreateSut()
      .Handle(MakeCommand(scartation: null, stay: null), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Guest stored = (await Db.Guests.FindAsync(result.Value))!;
    stored.Scartation.ShouldBeNull();
  }
}
