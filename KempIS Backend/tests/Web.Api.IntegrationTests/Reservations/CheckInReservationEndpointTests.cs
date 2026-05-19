using Application.Abstractions.Authentication;
using Domain.Common;
using Domain.Reservations.Guests;
using Domain.Reservations.Nationalities;
using Domain.Reservations.ReservationStates;
using TestUtilities.Builders;
using Web.Api.IntegrationTests.Infrastructure;
using DomainReservation = Domain.Reservations.Reservations.Reservation;

namespace Web.Api.IntegrationTests.Reservations;

public sealed class CheckInReservationEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public CheckInReservationEndpointTests(ApiFactory factory) => _factory = factory;

  public Task InitializeAsync() => _factory.ResetAllAsync();
  public Task DisposeAsync() => Task.CompletedTask;

  private HttpClient Client(params string[] roles)
  {
    HttpClient c = _factory.CreateClient();
    if (roles.Length > 0)
    {
      c.DefaultRequestHeaders.Add("X-Test-Roles", string.Join(",", roles));
    }
    return c;
  }

  [Fact]
  public async Task CheckIn_NonCzechGuestMissingSignature_Returns400()
  {
    DomainReservation reservation = new ReservationBuilder().InState(ReservationState.Confirmed).Build();
    Nationality? de = null;
    await _factory.WithDbAsync(async db =>
    {
      de = await db.Nationalities.SingleOrDefaultAsync(n => n.Alpha2 == "DE");
      if (de is null)
      {
        de = new Nationality
        {
          Id = Guid.NewGuid(),
          Name = "DE",
          NameEn = "Test",
          Alpha2 = "DE",
          Alpha3 = "DEU",
          Numeric = "276",
          VisaRequired = false,
          BiometricsRequired = false,
          IsEu = true,
          LanguageId = Guid.NewGuid(),
        };
        db.Nationalities.Add(de);
      }
      db.Reservations.Add(reservation);
      db.Guests.Add(new Guest
      {
        Id = Guid.NewGuid(),
        ReservationId = reservation.Id,
        FirstName = "A",
        LastName = "B",
        NationalityId = de.Id,
        DateOfBirth = new DateOnly(1990, 1, 1),
        DocumentType = DocumentType.Passport,
        DocumentNumber = "P1",
        Address = new Address(Guid.NewGuid(), "City", "12345", "Street", "1"),
        ReasonOfStay = "tourism",
        StayDateRange = new DateRange(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5)),
      });
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client(Roles.Receptionist).PostAsync(
      new Uri($"reservations/{reservation.Id}/check-in", UriKind.Relative), content: null);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    await _factory.WithDbAsync(async db =>
    {
      DomainReservation reloaded = await db.Reservations.AsNoTracking().SingleAsync(x => x.Id == reservation.Id);
      reloaded.State.ShouldBe(ReservationState.Confirmed);
    });
  }

  [Fact]
  public async Task CheckIn_AllSignedOrCzech_Returns204()
  {
    DomainReservation reservation = new ReservationBuilder().InState(ReservationState.Confirmed).Build();
    Nationality? cz = null;
    await _factory.WithDbAsync(async db =>
    {
      cz = await db.Nationalities.SingleOrDefaultAsync(n => n.Alpha2 == "CZ");
      if (cz is null)
      {
        cz = new Nationality
        {
          Id = Guid.NewGuid(),
          Name = "CZ",
          NameEn = "Test",
          Alpha2 = "CZ",
          Alpha3 = "CZE",
          Numeric = "203",
          VisaRequired = false,
          BiometricsRequired = false,
          IsEu = true,
          LanguageId = Guid.NewGuid(),
        };
        db.Nationalities.Add(cz);
      }
      db.Reservations.Add(reservation);
      db.Guests.Add(new Guest
      {
        Id = Guid.NewGuid(),
        ReservationId = reservation.Id,
        FirstName = "A",
        LastName = "B",
        NationalityId = cz.Id,
        DateOfBirth = new DateOnly(1990, 1, 1),
        DocumentType = DocumentType.Passport,
        DocumentNumber = "P1",
        Address = new Address(Guid.NewGuid(), "City", "12345", "Street", "1"),
        ReasonOfStay = "tourism",
        StayDateRange = new DateRange(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5)),
      });
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client(Roles.Receptionist).PostAsync(
      new Uri($"reservations/{reservation.Id}/check-in", UriKind.Relative), content: null);

    response.StatusCode.ShouldBe(
      HttpStatusCode.NoContent,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");
    await _factory.WithDbAsync(async db =>
    {
      DomainReservation reloaded = await db.Reservations.AsNoTracking().SingleAsync(x => x.Id == reservation.Id);
      reloaded.State.ShouldBe(ReservationState.CheckedIn);
    });
  }
}
