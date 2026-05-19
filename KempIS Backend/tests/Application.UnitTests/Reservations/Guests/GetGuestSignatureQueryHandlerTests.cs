using Application.Reservations.Guests.Queries.GetGuestSignature;
using Domain.Common;
using Domain.Reservations.Guests;
using SharedKernel;

namespace Application.UnitTests.Reservations.Guests;

public sealed class GetGuestSignatureQueryHandlerTests : HandlerTestBase
{
  private GetGuestSignatureQueryHandler CreateSut() => new(Db);

  private async Task<Guest> SeedGuest(byte[]? signature)
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
      SignaturePng = signature,
    };
    Db.Guests.Add(g);
    await Db.SaveChangesAsync();
    return g;
  }

  [Fact]
  public async Task Handle_SignaturePresent_ReturnsBytes()
  {
    byte[] expected = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x42];
    Guest g = await SeedGuest(expected);

    Result<GetGuestSignatureResponse> result = await CreateSut().Handle(
      new GetGuestSignatureQuery(g.Id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Content.ShouldBe(expected);
  }

  [Fact]
  public async Task Handle_SignatureNotSet_ReturnsSignatureNotFound()
  {
    Guest g = await SeedGuest(null);

    Result<GetGuestSignatureResponse> result = await CreateSut().Handle(
      new GetGuestSignatureQuery(g.Id), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(GuestErrors.SignatureNotFound(g.Id));
  }

  [Fact]
  public async Task Handle_GuestMissing_ReturnsSignatureNotFound()
  {
    var missing = Guid.NewGuid();

    Result<GetGuestSignatureResponse> result = await CreateSut().Handle(
      new GetGuestSignatureQuery(missing), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(GuestErrors.SignatureNotFound(missing));
  }
}
