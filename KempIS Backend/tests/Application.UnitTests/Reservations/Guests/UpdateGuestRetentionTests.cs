using Application.Reservations.Guests;
using Domain.Common;
using Domain.Reservations.Guests;
using SharedKernel;

namespace Application.UnitTests.Reservations.Guests;

public sealed class UpdateGuestRetentionTests : HandlerTestBase
{
  private static Address Addr() => new(Guid.NewGuid(), "Prague", "10000", "Main", "1");

  private UpdateGuestCommandHandler CreateSut() => new(Db, Clock);

  private async Task<Guest> SeedGuest(DateOnly? scartation)
  {
    Guest g = new()
    {
      Id = Guid.NewGuid(),
      ReservationId = Guid.NewGuid(),
      FirstName = "John",
      LastName = "Doe",
      NationalityId = Guid.NewGuid(),
      DateOfBirth = new DateOnly(1990, 1, 1),
      DocumentType = DocumentType.IdCard,
      DocumentNumber = "D1",
      Address = Addr(),
      ReasonOfStay = "Holiday",
      StayDateRange = new DateRange(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5)),
      Scartation = scartation,
    };
    Db.Guests.Add(g);
    await Db.SaveChangesAsync();
    return g;
  }

  private static UpdateGuestCommand BuildCommand(Guest g, DateOnly? scartation) => new(
    Id: g.Id,
    ReservationId: g.ReservationId!.Value,
    BillId: null,
    PaysRecreationFee: null,
    FirstName: g.FirstName,
    LastName: g.LastName,
    NationalityId: g.NationalityId,
    DateOfBirth: g.DateOfBirth,
    DocumentType: g.DocumentType,
    DocumentNumber: g.DocumentNumber,
    Address: g.Address,
    ReasonOfStay: g.ReasonOfStay,
    StayDateRange: g.StayDateRange,
    VisaNumber: null,
    Note: null,
    Scartation: scartation,
    CheckInAt: null,
    CheckOutAt: null,
    SignaturePngBase64: null);

  [Fact]
  public async Task Handle_NullScartationOnUpdate_PreservesExistingValue()
  {
    var existing = new DateOnly(2030, 1, 1);
    Guest seeded = await SeedGuest(existing);

    Result result = await CreateSut()
      .Handle(BuildCommand(seeded, scartation: null), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Guest reloaded = await Db.Guests.AsNoTracking().SingleAsync(g => g.Id == seeded.Id);
    reloaded.Scartation.ShouldBe(existing);
  }

  [Fact]
  public async Task Handle_ExplicitScartationOnUpdate_Overrides()
  {
    var existing = new DateOnly(2030, 1, 1);
    var replacement = new DateOnly(2040, 6, 15);
    Guest seeded = await SeedGuest(existing);

    Result result = await CreateSut()
      .Handle(BuildCommand(seeded, scartation: replacement), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Guest reloaded = await Db.Guests.AsNoTracking().SingleAsync(g => g.Id == seeded.Id);
    reloaded.Scartation.ShouldBe(replacement);
  }
}
