using System.Text.Json;
using Application.Reservations.Commands.CreateWebReservation;
using SharedKernel;
using TestUtilities.Builders;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Reservations;

public sealed class WebReservationEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

  private readonly ApiFactory _factory;
  private readonly HttpClient _client;

  public WebReservationEndpointTests(ApiFactory factory)
  {
    _factory = factory;
    _client = factory.CreateClient();
  }

  public Task InitializeAsync() => _factory.ResetReservationsAsync();
  public Task DisposeAsync() => Task.CompletedTask;

  [Fact]
  public async Task PostWeb_Anonymous_CreatesReservation_Returns201_DoesNotExposeSecret()
  {
    var groupId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.SpotGroups.Add(new SpotGroupBuilder().WithId(groupId).WithCapacity(5).Build());
      for (int i = 0; i < 5; i++)
      {
        db.Spots.Add(new SpotBuilder().InGroup(groupId).Build());
      }
      await db.SaveChangesAsync();
    });

    var request = new
    {
      Name = "Jan",
      Surname = "Novak",
      Email = "jan@example.com",
      Phone = "+420111000",
      From = new DateOnly(2026, 7, 10),
      To = new DateOnly(2026, 7, 12),
      RequestedSpots = new[] { new { SpotGroupId = groupId, Quantity = 1u } },
      Note = (string?)null,
      GroupReservationId = (Guid?)null,
      GroupReservationSecret = (string?)null,
    };

    HttpResponseMessage response = await _client.PostAsJsonAsync(
      new Uri("reservations/web", UriKind.Relative), request);

    response.StatusCode.ShouldBe(HttpStatusCode.Created);
    string rawBody = await response.Content.ReadAsStringAsync();
    CreateWebReservationResponse? body = JsonSerializer.Deserialize<CreateWebReservationResponse>(rawBody, WebJsonOptions);
    body.ShouldNotBeNull();
    response.Headers.Location.ShouldNotBeNull();
    response.Headers.Location!.ToString().ShouldContain(body.Id.ToString());

    rawBody.ShouldNotContain("secret", Case.Insensitive);
    response.Headers.Location.ToString().ShouldNotContain("secret", Case.Insensitive);
  }

  [Fact]
  public async Task PostWeb_InvalidPayload_Returns400()
  {
    var request = new
    {
      Name = "",
      Surname = "",
      Email = "not-an-email",
      Phone = "",
      From = new DateOnly(2026, 7, 15),
      To = new DateOnly(2026, 7, 10),
      RequestedSpots = Array.Empty<object>(),
      Note = (string?)null,
      GroupReservationId = (Guid?)null,
      GroupReservationSecret = (string?)null,
    };

    HttpResponseMessage response = await _client.PostAsJsonAsync(
      new Uri("reservations/web", UriKind.Relative), request);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }
}
