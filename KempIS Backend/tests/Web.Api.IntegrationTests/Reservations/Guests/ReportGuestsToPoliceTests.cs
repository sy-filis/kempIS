using Application.Abstractions.Reservations;
using Domain.Common;
using Domain.Reservations.Guests;
using Domain.Reservations.Nationalities;
using Domain.Reservations.PoliceReports;
using SharedKernel;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Reservations.Guests;

public sealed class ReportGuestsToPoliceTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public ReportGuestsToPoliceTests(ApiFactory factory) => _factory = factory;

  public async Task InitializeAsync()
  {
    await _factory.ResetAllAsync();
    _factory.PoliceGuestReporter.ClearReceivedCalls();
  }

  public Task DisposeAsync() => Task.CompletedTask;

  private HttpClient Client(params string[] roles)
  {
    HttpClient c = _factory.CreateClient();
    c.DefaultRequestHeaders.Add("X-Test-Roles", string.Join(",", roles));
    return c;
  }

  private async Task<Nationality> SeedNationality(string alpha2)
  {
    Nationality? existing = null;
    await _factory.WithDbAsync(async db =>
    {
      existing = await db.Nationalities.FirstOrDefaultAsync(x => x.Alpha2 == alpha2);
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
      Numeric = "000",
      VisaRequired = false,
      BiometricsRequired = false,
      IsEu = false,
      LanguageId = Guid.NewGuid(),
    };
    await _factory.WithDbAsync(async db => { db.Nationalities.Add(n); await db.SaveChangesAsync(); });
    return n;
  }

  private static readonly DateTime DefaultCheckInAt = new(2026, 5, 1, 15, 0, 0, DateTimeKind.Utc);

  private async Task<Guest> SeedGuest(Guid nationalityId, DateTime? checkInAt = default, bool hasCheckIn = true)
  {
    Guest g = new()
    {
      Id = Guid.NewGuid(),
      ReservationId = Guid.NewGuid(),
      FirstName = "A",
      LastName = "B",
      NationalityId = nationalityId,
      DateOfBirth = new DateOnly(1990, 1, 1),
      DocumentType = DocumentType.Passport,
      DocumentNumber = "P1",
      Address = new Address(Guid.NewGuid(), "Berlin", "10115", "Hauptstr", "1"),
      ReasonOfStay = "tourism",
      StayDateRange = new DateRange(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5)),
      CheckInAt = hasCheckIn ? (checkInAt ?? DefaultCheckInAt) : null,
    };
    await _factory.WithDbAsync(async db => { db.Guests.Add(g); await db.SaveChangesAsync(); });
    return g;
  }

  [Fact]
  public async Task Post_Anonymous_Returns401()
  {
    HttpResponseMessage response = await _factory.CreateClient().PostAsync("guests/report-to-police", null);
    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Post_AsAccountant_Returns403()
  {
    HttpResponseMessage response = await Client("Accountant").PostAsync("guests/report-to-police", null);
    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task Post_WithNoGuests_Returns204AndReporterNotCalled()
  {
    HttpResponseMessage response = await Client("Receptionist").PostAsync("guests/report-to-police", null);
    response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    await _factory.PoliceGuestReporter.DidNotReceiveWithAnyArgs().SubmitAsync(default!, default);
  }

  [Fact]
  public async Task Post_WithOnlyCzechGuests_Returns204AndReporterNotCalled()
  {
    Nationality cz = await SeedNationality("CZ");
    await SeedGuest(cz.Id);

    HttpResponseMessage response = await Client("Manager").PostAsync("guests/report-to-police", null);
    response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    await _factory.PoliceGuestReporter.DidNotReceiveWithAnyArgs().SubmitAsync(default!, default);
  }

  [Fact]
  public async Task Post_WithUnreportedForeignGuest_Returns204AndStampsReportedAt()
  {
    Nationality de = await SeedNationality("DE");
    Guest g = await SeedGuest(de.Id);
    _factory.PoliceGuestReporter.SubmitAsync(default!, default).ReturnsForAnyArgs(Result.Success());

    HttpResponseMessage response = await Client("Receptionist").PostAsync("guests/report-to-police", null);

    string err = _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception";
    response.StatusCode.ShouldBe(HttpStatusCode.NoContent, err);
    await _factory.WithDbAsync(async db =>
    {
      Guest reloaded = await db.Guests.SingleAsync(x => x.Id == g.Id);
      reloaded.ReportedAt.ShouldNotBeNull();
    });
  }

  [Fact]
  public async Task Post_WhenReporterReturnsUnauthorized_Returns500AndLeavesReportedAtUnchanged()
  {
    Nationality de = await SeedNationality("DE");
    Guest g = await SeedGuest(de.Id);
    _factory.PoliceGuestReporter.SubmitAsync(default!, default)
      .ReturnsForAnyArgs(Result.Failure(PoliceReportErrors.Unauthorized));

    HttpResponseMessage response = await Client("Receptionist").PostAsync("guests/report-to-police", null);
    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    await _factory.WithDbAsync(async db =>
    {
      Guest reloaded = await db.Guests.SingleAsync(x => x.Id == g.Id);
      reloaded.ReportedAt.ShouldBeNull();
    });
  }

  [Fact]
  public async Task Post_WhenReporterReturnsUnavailable_Returns500()
  {
    Nationality de = await SeedNationality("DE");
    await SeedGuest(de.Id);
    _factory.PoliceGuestReporter.SubmitAsync(default!, default)
      .ReturnsForAnyArgs(Result.Failure(PoliceReportErrors.Unavailable));

    HttpResponseMessage response = await Client("Receptionist").PostAsync("guests/report-to-police", null);
    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
  }

  [Fact]
  public async Task Post_WithOnlineCheckInOnly_IsSkipped()
  {
    Nationality de = await SeedNationality("DE");
    await SeedGuest(de.Id, hasCheckIn: false);

    HttpResponseMessage response = await Client("Receptionist").PostAsync("guests/report-to-police", null);

    string err = _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception";
    response.StatusCode.ShouldBe(HttpStatusCode.NoContent, err);
    await _factory.PoliceGuestReporter.DidNotReceiveWithAnyArgs().SubmitAsync(default!, default);
  }
}
