using Application.Abstractions.Authentication;
using Domain.Common;
using Domain.Reservations.Guests;
using Domain.Reservations.Nationalities;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Reservations.Guests;

public sealed class GuestSignatureEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public GuestSignatureEndpointTests(ApiFactory factory) => _factory = factory;

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

  private static readonly byte[] PngMagic =
    [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

  private static string ValidPngBase64()
  {
    byte[] bytes = new byte[PngMagic.Length + 16];
    Array.Copy(PngMagic, bytes, PngMagic.Length);
    return Convert.ToBase64String(bytes);
  }

  private async Task<(Guest Guest, Nationality Nationality)> SeedGuestWithNationality(string alpha2)
  {
    Nationality? nationality = null;
    Guest? guest = null;

    await _factory.WithDbAsync(async db =>
    {
      nationality = await db.Nationalities.SingleOrDefaultAsync(n => n.Alpha2 == alpha2);
      if (nationality is null)
      {
        nationality = new Nationality
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
        db.Nationalities.Add(nationality);
      }
      guest = new Guest
      {
        Id = Guid.NewGuid(),
        ReservationId = Guid.NewGuid(),
        FirstName = "A",
        LastName = "B",
        NationalityId = nationality.Id,
        DateOfBirth = new DateOnly(1990, 1, 1),
        DocumentType = DocumentType.Passport,
        DocumentNumber = "P1",
        Address = new Address(Guid.NewGuid(), "City", "12345", "Street", "1"),
        ReasonOfStay = "tourism",
        StayDateRange = new DateRange(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5)),
      };
      db.Guests.Add(guest);
      await db.SaveChangesAsync();
    });

    return (guest!, nationality!);
  }

  [Fact]
  public async Task Put_AsReceptionist_Returns204AndPersists()
  {
    (Guest guest, _) = await SeedGuestWithNationality("DE");

    HttpResponseMessage response = await Client(Roles.Receptionist).PutAsJsonAsync(
      new Uri($"guests/{guest.Id}/signature", UriKind.Relative),
      new { SignaturePngBase64 = ValidPngBase64() });

    response.StatusCode.ShouldBe(
      HttpStatusCode.NoContent,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    await _factory.WithDbAsync(async db =>
    {
      Guest reloaded = await db.Guests.AsNoTracking().SingleAsync(g => g.Id == guest.Id);
      reloaded.SignaturePng.ShouldNotBeNull();
      reloaded.SignatureCapturedAtUtc.ShouldNotBeNull();
    });
  }

  [Fact]
  public async Task Put_Anonymous_Returns401()
  {
    (Guest guest, _) = await SeedGuestWithNationality("DE");

    HttpResponseMessage response = await Client().PutAsJsonAsync(
      new Uri($"guests/{guest.Id}/signature", UriKind.Relative),
      new { SignaturePngBase64 = ValidPngBase64() });

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Get_AfterPut_ReturnsImagePng()
  {
    (Guest guest, _) = await SeedGuestWithNationality("DE");
    HttpClient client = Client(Roles.Receptionist);

    await client.PutAsJsonAsync(
      new Uri($"guests/{guest.Id}/signature", UriKind.Relative),
      new { SignaturePngBase64 = ValidPngBase64() });

    HttpResponseMessage response = await client.GetAsync(
      new Uri($"guests/{guest.Id}/signature", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    response.Content.Headers.ContentType?.MediaType.ShouldBe("image/png");
    byte[] bytes = await response.Content.ReadAsByteArrayAsync();
    bytes[0..PngMagic.Length].ShouldBe(PngMagic);
  }

  [Fact]
  public async Task Get_NoSignature_Returns404()
  {
    (Guest guest, _) = await SeedGuestWithNationality("DE");

    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri($"guests/{guest.Id}/signature", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task Delete_AfterPut_RemovesSignature()
  {
    (Guest guest, _) = await SeedGuestWithNationality("DE");
    HttpClient client = Client(Roles.Receptionist);

    await client.PutAsJsonAsync(
      new Uri($"guests/{guest.Id}/signature", UriKind.Relative),
      new { SignaturePngBase64 = ValidPngBase64() });

    HttpResponseMessage delete = await client.DeleteAsync(
      new Uri($"guests/{guest.Id}/signature", UriKind.Relative));
    delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

    await _factory.WithDbAsync(async db =>
    {
      Guest reloaded = await db.Guests.AsNoTracking().SingleAsync(g => g.Id == guest.Id);
      reloaded.SignaturePng.ShouldBeNull();
    });
  }

  [Fact]
  public async Task Put_CzechGuest_DropsSilently()
  {
    (Guest guest, _) = await SeedGuestWithNationality("CZ");

    HttpResponseMessage response = await Client(Roles.Receptionist).PutAsJsonAsync(
      new Uri($"guests/{guest.Id}/signature", UriKind.Relative),
      new { SignaturePngBase64 = ValidPngBase64() });

    response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    await _factory.WithDbAsync(async db =>
    {
      Guest reloaded = await db.Guests.AsNoTracking().SingleAsync(g => g.Id == guest.Id);
      reloaded.SignaturePng.ShouldBeNull();
    });
  }
}
