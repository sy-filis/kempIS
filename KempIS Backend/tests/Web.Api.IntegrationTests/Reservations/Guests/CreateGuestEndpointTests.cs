using Application.Abstractions.Authentication;
using Application.Reservations.Queries.GetReservationById;
using Domain.Reservations.Nationalities;
using TestUtilities.Builders;
using Web.Api.IntegrationTests.Infrastructure;
using DomainReservation = Domain.Reservations.Reservations.Reservation;

namespace Web.Api.IntegrationTests.Reservations.Guests;

public sealed class CreateGuestEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public CreateGuestEndpointTests(ApiFactory factory) => _factory = factory;

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
  public async Task Post_StaffSubmissionWithEmptyOptionalFields_Returns201AndPersistsThem()
  {
    DomainReservation reservation = new ReservationBuilder().Build();
    Guid nationalityId = Guid.Empty;

    await _factory.WithDbAsync(async db =>
    {
      db.Reservations.Add(reservation);
      Nationality nationality = await db.Nationalities.AsNoTracking().FirstAsync();
      nationalityId = nationality.Id;
      await db.SaveChangesAsync();
    });

    object payload = new
    {
      reservationId = reservation.Id,
      billId = (Guid?)null,
      firstName = "Jan",
      lastName = "Novak",
      nationalityId,
      dateOfBirth = "1990-05-10",
      documentType = (int?)null,
      documentNumber = (string?)null,
      address = new
      {
        countryId = nationalityId,
        city = "Praha",
        zipCode = "120 00",
        street = "Stepanska",
        houseNumber = "12",
      },
      reasonOfStay = "",
      stayDateRange = (object?)null,
      visaNumber = (string?)null,
      note = (string?)null,
      scartation = (string?)null,
      checkInAt = (string?)null,
      checkOutAt = (string?)null,
      signaturePngBase64 = (string?)null,
    };

    HttpResponseMessage createResponse = await Client(Roles.Receptionist)
      .PostAsJsonAsync(new Uri("guests", UriKind.Relative), payload);

    createResponse.StatusCode.ShouldBe(
      HttpStatusCode.Created,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    HttpResponseMessage detailResponse = await Client(Roles.Receptionist).GetAsync(
      new Uri($"reservations/{reservation.Id}", UriKind.Relative));

    detailResponse.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex2) ? ex2.ToString() : "no exception");

    ReservationDetailResponse? body = await detailResponse.Content.ReadFromJsonAsync<ReservationDetailResponse>();
    body.ShouldNotBeNull();
    body.Guests.Count.ShouldBe(1);
    ReservationDetailGuest persisted = body.Guests[0];
    persisted.FirstName.ShouldBe("Jan");
    persisted.LastName.ShouldBe("Novak");
    persisted.DocumentType.ShouldBeNull();
    persisted.DocumentNumber.ShouldBeNull();
    persisted.ReasonOfStay.ShouldBe("");
    persisted.StayFrom.ShouldBeNull();
    persisted.StayTo.ShouldBeNull();
  }
}
