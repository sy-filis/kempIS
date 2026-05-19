using Domain.Common;
using Domain.Reservations.Guests;
using Infrastructure.Database;
using Microsoft.Data.Sqlite;
using TestUtilities.Fakes;

namespace Application.UnitTests.Reservations.Guests;

public sealed class GuestTimestampTests : HandlerTestBase
{
  private static Guest BuildGuest() => new()
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
  };

  [Fact]
  public async Task SaveChanges_OnAdded_StampsCreatedAtAndUpdatedAt()
  {
    DateTime fixedNow = new(2026, 4, 21, 10, 0, 0, DateTimeKind.Utc);
    Clock.Set(fixedNow);

    Guest g = BuildGuest();
    Db.Guests.Add(g);
    await Db.SaveChangesAsync();

    g.CreatedAt.ShouldBe(fixedNow);
    g.UpdatedAt.ShouldBe(fixedNow);
  }

  [Fact]
  public async Task SaveChanges_OnModified_StampsOnlyUpdatedAt()
  {
    DateTime create = new(2026, 4, 21, 10, 0, 0, DateTimeKind.Utc);
    DateTime update = new(2026, 4, 22, 10, 0, 0, DateTimeKind.Utc);

    Clock.Set(create);
    Guest g = BuildGuest();
    Db.Guests.Add(g);
    await Db.SaveChangesAsync();

    Clock.Set(update);
    g.Note = "edited";
    await Db.SaveChangesAsync();

    g.CreatedAt.ShouldBe(create);
    g.UpdatedAt.ShouldBe(update);
  }
}
