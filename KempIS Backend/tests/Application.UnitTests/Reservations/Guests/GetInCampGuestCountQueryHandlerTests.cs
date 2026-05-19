using Application.Reservations.Guests;
using Domain.Common;
using Domain.Reservations.Guests;
using SharedKernel;

namespace Application.UnitTests.Reservations.Guests;

public sealed class GetInCampGuestCountQueryHandlerTests : HandlerTestBase
{
  private GetInCampGuestCountQueryHandler CreateSut() => new(Db);

  private static Guest BuildGuest(DateTime? checkInAt, DateTime? checkOutAt) => new()
  {
    Id = Guid.NewGuid(),
    ReservationId = Guid.NewGuid(),
    FirstName = "A",
    LastName = "B",
    NationalityId = Guid.NewGuid(),
    DateOfBirth = new DateOnly(1990, 1, 1),
    DocumentType = DocumentType.Passport,
    DocumentNumber = "X",
    Address = new Address(Guid.NewGuid(), "City", "12345", "Street", "1"),
    ReasonOfStay = "tourism",
    StayDateRange = new DateRange(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5)),
    CheckInAt = checkInAt,
    CheckOutAt = checkOutAt,
  };

  [Fact]
  public async Task Handle_NoGuests_ReturnsZero()
  {
    Result<int> result = await CreateSut().Handle(new GetInCampGuestCountQuery(), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_CountsOnlyGuestsCheckedInAndNotCheckedOut()
  {
    var t = new DateTime(2026, 5, 2, 12, 0, 0, DateTimeKind.Utc);

    Db.Guests.Add(BuildGuest(checkInAt: null, checkOutAt: null));
    Db.Guests.Add(BuildGuest(checkInAt: t, checkOutAt: t.AddHours(1)));
    Db.Guests.Add(BuildGuest(checkInAt: t, checkOutAt: null));
    Db.Guests.Add(BuildGuest(checkInAt: t.AddDays(-1), checkOutAt: null));
    Db.Guests.Add(BuildGuest(checkInAt: null, checkOutAt: t));
    await Db.SaveChangesAsync();

    Result<int> result = await CreateSut().Handle(new GetInCampGuestCountQuery(), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBe(2);
  }
}
