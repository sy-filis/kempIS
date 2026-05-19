using Application.Abstractions.Authentication;
using Domain.Reservations.ReservationStates;
using SharedKernel;
using TestUtilities.Builders;
using Web.Api.IntegrationTests.Infrastructure;
using DomainReservation = Domain.Reservations.Reservations.Reservation;

namespace Web.Api.IntegrationTests.Reservations;

public sealed class ReservationConfirmationEmailTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  private Guid _groupId = Guid.NewGuid();
  private Guid _spotId = Guid.NewGuid();

  public ReservationConfirmationEmailTests(ApiFactory factory) => _factory = factory;

  public async Task InitializeAsync()
  {
    await _factory.ResetReservationsAsync();
    _factory.EmailSender.Clear();

    _groupId = Guid.NewGuid();
    _spotId = Guid.NewGuid();

    await _factory.WithDbAsync(async db =>
    {
      db.SpotGroups.Add(new SpotGroupBuilder().WithId(_groupId).WithCapacity(5).Build());
      db.Spots.Add(new SpotBuilder().WithId(_spotId).InGroup(_groupId).Build());
      await db.SaveChangesAsync();
    });

    _factory.AvailabilityChecker.CheckAsync(default!, default!, default!, default)
      .ReturnsForAnyArgs(Result.Success());
  }

  public Task DisposeAsync() => Task.CompletedTask;

  [Fact]
  public async Task UpdateReservation_OnCreatedToConfirmedTransition_SendsExactlyOneEmail()
  {
    DomainReservation reservation = new ReservationBuilder()
      .InState(ReservationState.Created)
      .MadeBy("Petra", "Novakova", "petra@example.com", "+420111222333")
      .For(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 5))
      .Build();

    await _factory.WithDbAsync(async db =>
    {
      db.Reservations.Add(reservation);
      await db.SaveChangesAsync();
    });

    var request = new
    {
      Name = "Petra",
      Surname = "Novakova",
      Email = "petra@example.com",
      Phone = "+420111222333",
      From = new DateOnly(2026, 7, 1),
      To = new DateOnly(2026, 7, 5),
      Note = (string?)null,
      GroupReservationId = (Guid?)null,
      SpotIds = new[] { _spotId },
      Services = Array.Empty<object>(),
      Vehicles = Array.Empty<object>(),
    };

    HttpResponseMessage response = await Client(Roles.Manager).PutAsJsonAsync(
      new Uri($"reservations/{reservation.Id}", UriKind.Relative), request);

    response.StatusCode.ShouldBe(HttpStatusCode.NoContent,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    _factory.EmailSender.Sent.Count.ShouldBe(1);
    _factory.EmailSender.Only.To.ShouldBe("petra@example.com");
  }

  [Fact]
  public async Task UpdateReservation_WhenAlreadyConfirmed_DoesNotSendEmail()
  {
    DomainReservation reservation = new ReservationBuilder()
      .InState(ReservationState.Confirmed)
      .MadeBy("Tomas", "Kral", "tomas@example.com", "+420444555666")
      .For(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 4))
      .Build();

    await _factory.WithDbAsync(async db =>
    {
      db.Reservations.Add(reservation);
      await db.SaveChangesAsync();
    });

    _factory.EmailSender.Clear();

    var request = new
    {
      Name = "Tomas",
      Surname = "Kral",
      Email = "tomas@example.com",
      Phone = "+420444555666",
      From = new DateOnly(2026, 8, 1),
      To = new DateOnly(2026, 8, 4),
      Note = (string?)null,
      GroupReservationId = (Guid?)null,
      SpotIds = new[] { _spotId },
      Services = Array.Empty<object>(),
      Vehicles = Array.Empty<object>(),
    };

    HttpResponseMessage response = await Client(Roles.Manager).PutAsJsonAsync(
      new Uri($"reservations/{reservation.Id}", UriKind.Relative), request);

    response.StatusCode.ShouldBe(HttpStatusCode.NoContent,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    _factory.EmailSender.Sent.ShouldBeEmpty();
  }

  [Fact]
  public async Task CreateReservation_StaffCreatesConfirmed_SendsConfirmationEmail()
  {
    var request = new
    {
      Name = "Anna",
      Surname = "Blazkova",
      Email = "anna@example.com",
      Phone = "+420777888999",
      From = new DateOnly(2026, 9, 10),
      To = new DateOnly(2026, 9, 15),
      Note = (string?)null,
      GroupReservationId = (Guid?)null,
      SpotIds = new[] { _spotId },
      Services = Array.Empty<object>(),
      Vehicles = Array.Empty<object>(),
    };

    HttpResponseMessage response = await Client(Roles.Receptionist).PostAsJsonAsync(
      new Uri("reservations", UriKind.Relative), request);

    response.StatusCode.ShouldBe(HttpStatusCode.Created,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    _factory.EmailSender.Sent.Count.ShouldBe(1);
    _factory.EmailSender.Only.To.ShouldBe("anna@example.com");
  }

  [Fact]
  public async Task CreateWebReservation_DoesNotSendEmail()
  {
    var request = new
    {
      Name = "Lucie",
      Surname = "Mala",
      Email = "lucie@example.com",
      Phone = "+420123456789",
      From = new DateOnly(2026, 10, 1),
      To = new DateOnly(2026, 10, 4),
      RequestedSpots = new[] { new { SpotGroupId = _groupId, Quantity = 1u } },
      Note = (string?)null,
      GroupReservationId = (Guid?)null,
      GroupReservationSecret = (string?)null,
    };

    HttpResponseMessage response = await _factory.CreateClient().PostAsJsonAsync(
      new Uri("reservations/web", UriKind.Relative), request);

    response.StatusCode.ShouldBe(HttpStatusCode.Created,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    _factory.EmailSender.Sent.ShouldBeEmpty();
  }

  private HttpClient Client(params string[] roles)
  {
    HttpClient client = _factory.CreateClient();
    if (roles.Length > 0)
    {
      client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(",", roles));
    }
    return client;
  }
}
