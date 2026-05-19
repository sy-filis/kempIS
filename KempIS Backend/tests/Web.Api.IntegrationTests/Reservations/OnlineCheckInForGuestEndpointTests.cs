using Domain.Common;
using Domain.Reservations.Guests;
using Domain.Reservations.Nationalities;
using Domain.Reservations.Reservations;
using Domain.Reservations.ReservationStates;
using Microsoft.EntityFrameworkCore;
using TestUtilities.Builders;
using Web.Api.IntegrationTests.Infrastructure;
using DomainReservation = Domain.Reservations.Reservations.Reservation;

namespace Web.Api.IntegrationTests.Reservations;

public sealed class OnlineCheckInForGuestEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;
  private readonly HttpClient _client;

  public OnlineCheckInForGuestEndpointTests(ApiFactory factory)
  {
    _factory = factory;
    _client = factory.CreateClient();
  }

  public Task InitializeAsync() => _factory.ResetAllAsync();
  public Task DisposeAsync() => Task.CompletedTask;

  private async Task<(DomainReservation Reservation, Nationality Nationality)> SeedAsync(string secret)
  {
    DomainReservation reservation = new ReservationBuilder()
      .InState(ReservationState.Confirmed)
      .WithSecret(secret)
      .Build();
    Nationality nationality = null!;

    await _factory.WithDbAsync(async db =>
    {
      nationality = await db.Nationalities.SingleAsync(n => n.Alpha3 == "CZE");
      db.Reservations.Add(reservation);
      await db.SaveChangesAsync();
    });

    return (reservation, nationality);
  }

  private object BuildRequest(Guid nationalityId) => new
  {
    Guests = new[]
    {
      new
      {
        FirstName = "Jan",
        LastName = "Novak",
        BirthDate = new DateOnly(1990, 6, 15),
        NationalityId = nationalityId,
        DocumentType = (int)DocumentType.Passport,
        DocumentNumber = "AB123456",
        Address = new
        {
          CountryId = Guid.NewGuid(),
          City = "Praha",
          ZipCode = "11000",
          Street = "Vaclavske namesti",
          HouseNumber = "1"
        }
      }
    },
    Vehicles = new[]
    {
      new
      {
        RegistrationNumber = "1AB2345"
      }
    }
  };

  [Fact]
  public async Task OnlineCheckIn_WithValidSecret_Returns204()
  {
    (DomainReservation reservation, Nationality nationality) = await SeedAsync("valid-secret-abc");

    HttpResponseMessage response = await _client.PostAsJsonAsync(
      new Uri($"reservations/{reservation.Id}/guest/check-in?secret=valid-secret-abc", UriKind.Relative),
      BuildRequest(nationality.Id));

    response.StatusCode.ShouldBe(
      HttpStatusCode.NoContent,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    await _factory.WithDbAsync(async db =>
    {
      DomainReservation reloaded = await db.Reservations.AsNoTracking().SingleAsync(x => x.Id == reservation.Id);
      reloaded.OnlineCheckInStatus.ShouldBe(OnlineCheckInStatus.Completed);

      List<Guest> guests = await db.Guests.AsNoTracking()
        .Where(g => g.ReservationId == reservation.Id)
        .ToListAsync();
      guests.Count.ShouldBe(1);
      guests[0].FirstName.ShouldBe("Jan");

      List<Domain.Reservations.Vehicles.Vehicle> vehicles = await db.Vehicles.AsNoTracking()
        .Where(v => v.ReservationId == reservation.Id)
        .ToListAsync();
      vehicles.Count.ShouldBe(1);
      vehicles[0].RegistrationNumber.ShouldBe("1AB2345");
    });
  }

  [Fact]
  public async Task OnlineCheckIn_WithWrongSecret_Returns404()
  {
    (DomainReservation reservation, Nationality nationality) = await SeedAsync("correct-secret");

    HttpResponseMessage response = await _client.PostAsJsonAsync(
      new Uri($"reservations/{reservation.Id}/guest/check-in?secret=wrong-secret", UriKind.Relative),
      BuildRequest(nationality.Id));

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task OnlineCheckIn_Twice_Returns409()
  {
    (DomainReservation reservation, Nationality nationality) = await SeedAsync("double-checkin-secret");
    object request = BuildRequest(nationality.Id);

    HttpResponseMessage first = await _client.PostAsJsonAsync(
      new Uri($"reservations/{reservation.Id}/guest/check-in?secret=double-checkin-secret", UriKind.Relative),
      request);

    first.StatusCode.ShouldBe(
      HttpStatusCode.NoContent,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    HttpResponseMessage second = await _client.PostAsJsonAsync(
      new Uri($"reservations/{reservation.Id}/guest/check-in?secret=double-checkin-secret", UriKind.Relative),
      request);

    second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
  }

  [Fact]
  public async Task OnlineCheckIn_WithMalformedVisaNumber_Returns400()
  {
    (DomainReservation reservation, Nationality nationality) = await SeedAsync("malformed-visa");

    object request = new
    {
      Guests = new[]
      {
        new
        {
          FirstName = "Jan",
          LastName = "Novak",
          BirthDate = new DateOnly(1990, 6, 15),
          NationalityId = nationality.Id,
          DocumentType = (int)DocumentType.Passport,
          DocumentNumber = "AB123456",
          VisaNumber = "lowercase123",
          Address = new
          {
            CountryId = Guid.NewGuid(),
            City = "Praha",
            ZipCode = "11000",
            Street = "Vaclavske namesti",
            HouseNumber = "1",
          }
        }
      },
      Vehicles = Array.Empty<object>(),
    };

    HttpResponseMessage response = await _client.PostAsJsonAsync(
      new Uri($"reservations/{reservation.Id}/guest/check-in?secret=malformed-visa", UriKind.Relative),
      request);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task OnlineCheckIn_NonCzechWithoutSignature_Returns400()
  {
    (DomainReservation reservation, _) = await SeedAsync("non-cz-secret");
    Nationality de = await SeedNonCzechAsync("DE");

    object request = new
    {
      Guests = new[]
      {
        new
        {
          FirstName = "Hans",
          LastName = "Schmidt",
          BirthDate = new DateOnly(1990, 6, 15),
          NationalityId = de.Id,
          DocumentType = (int)DocumentType.Passport,
          DocumentNumber = "X1",
          Address = new
          {
            CountryId = Guid.NewGuid(),
            City = "Berlin",
            ZipCode = "10115",
            Street = "Hauptstr",
            HouseNumber = "1"
          },
          SignaturePngBase64 = (string?)null,
        }
      },
      Vehicles = Array.Empty<object>(),
    };

    HttpResponseMessage response = await _client.PostAsJsonAsync(
      new Uri($"reservations/{reservation.Id}/guest/check-in?secret=non-cz-secret", UriKind.Relative),
      request);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

    await _factory.WithDbAsync(async db =>
    {
      (await db.Guests.AsNoTracking().AnyAsync(g => g.ReservationId == reservation.Id)).ShouldBeFalse();
    });
  }

  private async Task<Nationality> SeedNonCzechAsync(string alpha2)
  {
    Nationality? existing = null;
    await _factory.WithDbAsync(async db =>
    {
      existing = await db.Nationalities.AsNoTracking().FirstOrDefaultAsync(n => n.Alpha2 == alpha2);
    });
    if (existing is not null)
    {
      return existing;
    }

    Nationality n = new()
    {
      Id = Guid.NewGuid(),
      Name = alpha2,
      NameEn = "Test",
      Alpha2 = alpha2,
      Alpha3 = alpha2.PadRight(3, 'X'),
      Numeric = "0" + alpha2,
      VisaRequired = false,
      BiometricsRequired = false,
      IsEu = false,
      LanguageId = Guid.NewGuid(),
    };
    await _factory.WithDbAsync(async db =>
    {
      db.Nationalities.Add(n);
      await db.SaveChangesAsync();
    });
    return n;
  }
}
