using Application.Reservations.Guests.Commands.ClearGuestSignature;
using Domain.Common;
using Domain.Reservations.Guests;
using SharedKernel;

namespace Application.UnitTests.Reservations.Guests;

public sealed class ClearGuestSignatureCommandHandlerTests : HandlerTestBase
{
  private ClearGuestSignatureCommandHandler CreateSut() => new(Db);

  private async Task<Guest> SeedGuestWithSignature()
  {
    Guest g = new()
    {
      Id = Guid.NewGuid(),
      ReservationId = Guid.NewGuid(),
      FirstName = "A",
      LastName = "B",
      NationalityId = Guid.NewGuid(),
      DateOfBirth = new DateOnly(1990, 1, 1),
      DocumentType = DocumentType.Passport,
      DocumentNumber = "P1",
      Address = new Address(Guid.NewGuid(), "City", "12345", "Street", "1"),
      ReasonOfStay = "tourism",
      StayDateRange = new DateRange(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5)),
      SignaturePng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0xAA],
      SignatureCapturedAtUtc = new DateTime(2026, 4, 27, 12, 0, 0, DateTimeKind.Utc),
    };
    Db.Guests.Add(g);
    await Db.SaveChangesAsync();
    return g;
  }

  [Fact]
  public async Task Handle_RemovesBytesAndTimestamp()
  {
    Guest g = await SeedGuestWithSignature();

    Result result = await CreateSut().Handle(
      new ClearGuestSignatureCommand(g.Id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Guest reloaded = await Db.Guests.AsNoTracking().SingleAsync(x => x.Id == g.Id);
    reloaded.SignaturePng.ShouldBeNull();
    reloaded.SignatureCapturedAtUtc.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_GuestNotFound_ReturnsNotFound()
  {
    var missing = Guid.NewGuid();

    Result result = await CreateSut().Handle(
      new ClearGuestSignatureCommand(missing), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(GuestErrors.NotFound(missing));
  }
}
