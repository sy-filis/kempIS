using System.Net;
using System.Net.Http.Json;
using Application.Abstractions.Authentication;
using Application.Abstractions.Gate;
using Domain.Finance.Payments;
using Domain.Reservations.Guests;
using Microsoft.EntityFrameworkCore;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Finance;

public sealed class CreateBillEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public CreateBillEndpointTests(ApiFactory factory) => _factory = factory;

  public Task InitializeAsync() => _factory.ResetAllAsync();
  public Task DisposeAsync() => Task.CompletedTask;

  private HttpClient Client(params string[] roles)
  {
    HttpClient client = _factory.CreateClient();
    if (roles.Length > 0)
    {
      client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(",", roles));
    }
    return client;
  }

  private static object WalkInBodyWithOneNewGuest() => new
  {
    reservationId = (Guid?)null,
    checkInAt = "2026-04-22",
    checkOutAt = "2026-04-23",
    payer = new { name = "John", surname = "Doe", address = MinimalAddress() },
    legalEntity = new { name = "Acme", cin = "123", tin = "CZ123", address = MinimalAddress() },
    paymentType = PaymentType.Cash,
    languageId = Guid.NewGuid(),
    items = new[]
    {
      new { serviceId = (Guid?)null, quantity = 1u, unitPrice = 300m, vatRatePercentage = 21m, recapSingleQuantity = 0u, recapDayQuantity = 0u },
    },
    linkedInvoiceIds = Array.Empty<Guid>(),
    existingGuests = Array.Empty<object>(),
    reservationSpotItemIds = Array.Empty<Guid>(),
    accessCards = Array.Empty<object>(),
    newVehicles = Array.Empty<object>(),
    existingVehicleIds = Array.Empty<Guid>(),
    newGuests = new[]
    {
      new
      {
        firstName = "Walk",
        lastName = "In",
        nationalityId = Guid.NewGuid(),
        dateOfBirth = "1990-01-01",
        documentType = (int)DocumentType.Passport,
        documentNumber = "D1",
        address = MinimalAddress(),
        reasonOfStay = "Holiday",
        stayFrom = "2026-04-22",
        stayTo = "2026-04-23",
        email = (string?)null,
        phoneNumber = (string?)null,
        supplementaryDocumentNumber = (string?)null,
        documentIssuerCountryCode = (string?)null,
        visaNumber = (string?)null,
        note = (string?)null,
        paysRecreationFee = true,
      },
    },
  };

  private static object MinimalAddress() => new
  {
    countryId = Guid.NewGuid(),
    city = "Prague",
    zipCode = "10000",
    street = "Main",
    houseNumber = "1",
  };

  [Fact]
  public async Task Post_Anonymous_Returns401()
  {
    using HttpClient client = _factory.CreateClient();
    HttpResponseMessage response = await client.PostAsJsonAsync("bills", WalkInBodyWithOneNewGuest());
    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Post_AsAccountant_Returns403()
  {
    HttpClient client = Client(Roles.Accountant);
    HttpResponseMessage response = await client.PostAsJsonAsync("bills", WalkInBodyWithOneNewGuest());
    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task Post_AsReceptionist_WalkIn_Returns201()
  {
    HttpClient client = Client(Roles.Receptionist);
    HttpResponseMessage response = await client.PostAsJsonAsync("bills", WalkInBodyWithOneNewGuest());

    string error = _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception";
    response.StatusCode.ShouldBe(HttpStatusCode.Created, error);

    Dictionary<string, object>? body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
    body.ShouldNotBeNull();
    body!.ShouldContainKey("billId");
    body.ShouldContainKey("number");
  }

  [Fact]
  public async Task Post_WithNoGuests_Returns201()
  {
    HttpClient client = Client(Roles.Receptionist);
    var body = new
    {
      reservationId = (Guid?)null,
      checkInAt = "2026-04-22",
      checkOutAt = "2026-04-23",
      payer = new { name = "John", surname = "Doe", address = MinimalAddress() },
      legalEntity = new { name = "Acme", cin = "123", tin = "CZ123", address = MinimalAddress() },
      paymentType = PaymentType.Cash,
      languageId = Guid.NewGuid(),
      items = new[]
      {
        new { serviceId = (Guid?)null, quantity = 1u, unitPrice = 300m, vatRatePercentage = 21m, recapSingleQuantity = 0u, recapDayQuantity = 0u },
      },
      linkedInvoiceIds = Array.Empty<Guid>(),
      existingGuests = Array.Empty<object>(),
      reservationSpotItemIds = Array.Empty<Guid>(),
      accessCards = Array.Empty<object>(),
      newVehicles = Array.Empty<object>(),
      existingVehicleIds = Array.Empty<Guid>(),
      newGuests = Array.Empty<object>(),
    };

    HttpResponseMessage response = await client.PostAsJsonAsync("bills", body);

    string error = _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception";
    response.StatusCode.ShouldBe(HttpStatusCode.Created, error);
  }

  [Fact]
  public async Task Post_WithSpotItemIds_LinksSpotsAndExposesBillIdInReservationDetail()
  {
    HttpClient receptionist = Client(Roles.Receptionist);

    var reservationId = Guid.NewGuid();
    var spotA = Guid.NewGuid();
    var spotB = Guid.NewGuid();
    var spotC = Guid.NewGuid();
    var guestId = Guid.NewGuid();

    await _factory.WithDbAsync(async db =>
    {
      db.Reservations.Add(new Domain.Reservations.Reservations.Reservation
      {
        Id = reservationId,
        Number = "R-1",
        Period = new Domain.Common.DateRange(new DateOnly(2026, 4, 20), new DateOnly(2026, 4, 22)),
        ReservationMaker = new Domain.Reservations.ReservationMakers.ReservationMaker(
          "M", "M", "m@example.com", "+420000000000"),
        State = Domain.Reservations.ReservationStates.ReservationState.Confirmed,
        CreatedAtUtc = DateTime.UtcNow,
        Secret = Guid.NewGuid().ToString("N"),
      });

      var spotGroupId = Guid.NewGuid();
      foreach (Guid id in new[] { spotA, spotB, spotC })
      {
        db.ReservationSpotItems.Add(new Domain.Reservations.ReservationSpotItems.ReservationSpotItem
        {
          Id = id,
          ReservationId = reservationId,
          SpotGroupId = spotGroupId,
          SpotId = Guid.NewGuid(),
        });
      }

      db.Guests.Add(new Domain.Reservations.Guests.Guest
      {
        Id = guestId,
        ReservationId = reservationId,
        FirstName = "F",
        LastName = "L",
        NationalityId = Guid.NewGuid(),
        DateOfBirth = new DateOnly(1990, 1, 1),
        DocumentType = Domain.Reservations.Guests.DocumentType.IdCard,
        DocumentNumber = "D1",
        Address = new Domain.Common.Address(Guid.NewGuid(), "Prague", "10000", "Main", "1"),
        ReasonOfStay = "Holiday",
      });

      await db.SaveChangesAsync();
    });

    var body = new
    {
      reservationId = (Guid?)reservationId,
      checkInAt = "2026-04-20",
      checkOutAt = "2026-04-22",
      payer = new { name = "John", surname = "Doe", address = MinimalAddress() },
      legalEntity = new { name = "Acme", cin = "123", tin = "CZ123", address = MinimalAddress() },
      paymentType = PaymentType.Card,
      languageId = Guid.NewGuid(),
      items = new[]
      {
        new { serviceId = (Guid?)null, quantity = 1u, unitPrice = 1000m, vatRatePercentage = 21m, recapSingleQuantity = 0u, recapDayQuantity = 0u },
      },
      linkedInvoiceIds = Array.Empty<Guid>(),
      existingGuests = new[] { new { guestId, paysRecreationFee = true } },
      reservationSpotItemIds = new[] { spotA, spotB },
      accessCards = Array.Empty<object>(),
      newVehicles = Array.Empty<object>(),
      existingVehicleIds = Array.Empty<Guid>(),
      newGuests = Array.Empty<object>(),
    };

    HttpResponseMessage response = await receptionist.PostAsJsonAsync("bills", body);
    string error = _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception";
    response.StatusCode.ShouldBe(HttpStatusCode.Created, error);

    Dictionary<string, object>? created = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
    Guid billId = ((System.Text.Json.JsonElement)created!["billId"]).GetGuid();

    HttpResponseMessage get = await receptionist.GetAsync($"reservations/{reservationId}");
    get.StatusCode.ShouldBe(HttpStatusCode.OK);

    using var doc =
      System.Text.Json.JsonDocument.Parse(await get.Content.ReadAsStringAsync());
    System.Text.Json.JsonElement spotItems = doc.RootElement.GetProperty("spotItems");

    Dictionary<Guid, Guid?> billByItem = [];
    foreach (System.Text.Json.JsonElement spot in spotItems.EnumerateArray())
    {
      Guid id = spot.GetProperty("id").GetGuid();
      Guid? linked = spot.TryGetProperty("billId", out System.Text.Json.JsonElement linkedEl)
        && linkedEl.ValueKind != System.Text.Json.JsonValueKind.Null
          ? linkedEl.GetGuid()
          : (Guid?)null;
      billByItem[id] = linked;
    }

    billByItem[spotA].ShouldBe(billId);
    billByItem[spotB].ShouldBe(billId);
    billByItem[spotC].ShouldBeNull();
  }

  [Fact]
  public async Task Post_WalkInWithSpotItems_Returns400()
  {
    HttpClient receptionist = Client(Roles.Receptionist);
    var body = new
    {
      reservationId = (Guid?)null,
      checkInAt = "2026-04-22",
      checkOutAt = "2026-04-23",
      payer = new { name = "John", surname = "Doe", address = MinimalAddress() },
      legalEntity = new { name = "Acme", cin = "123", tin = "CZ123", address = MinimalAddress() },
      paymentType = PaymentType.Cash,
      languageId = Guid.NewGuid(),
      items = new[]
      {
        new { serviceId = (Guid?)null, quantity = 1u, unitPrice = 100m, vatRatePercentage = 21m, recapSingleQuantity = 0u, recapDayQuantity = 0u },
      },
      linkedInvoiceIds = Array.Empty<Guid>(),
      existingGuests = Array.Empty<object>(),
      reservationSpotItemIds = new[] { Guid.NewGuid() },
      accessCards = Array.Empty<object>(),
      newVehicles = Array.Empty<object>(),
      existingVehicleIds = Array.Empty<Guid>(),
      newGuests = new[]
      {
        new
        {
          firstName = "Walk", lastName = "In",
          nationalityId = Guid.NewGuid(),
          dateOfBirth = "1990-01-01",
          documentType = (int)DocumentType.Passport,
          documentNumber = "D1",
          address = MinimalAddress(),
          reasonOfStay = "Holiday",
          stayFrom = "2026-04-22", stayTo = "2026-04-23",
          email = (string?)null, phoneNumber = (string?)null,
          supplementaryDocumentNumber = (string?)null,
          documentIssuerCountryCode = (string?)null,
          visaNumber = (string?)null, note = (string?)null,
          paysRecreationFee = true,
        },
      },
    };

    HttpResponseMessage response = await receptionist.PostAsJsonAsync("bills", body);
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Post_AccessCardWithZeroUid_Returns400()
  {
    HttpClient client = Client(Roles.Receptionist);
    object body = WalkInBodyWithAccessCards(new[]
    {
      new { uid = 0UL, deposit = 0m, validUntil = "2026-08-15", note = (string?)null },
    });

    HttpResponseMessage response = await client.PostAsJsonAsync("bills", body);
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Post_AccessCardWithNegativeDeposit_Returns400()
  {
    HttpClient client = Client(Roles.Receptionist);
    object body = WalkInBodyWithAccessCards(new[]
    {
      new { uid = 1UL, deposit = -1m, validUntil = "2026-08-15", note = (string?)null },
    });

    HttpResponseMessage response = await client.PostAsJsonAsync("bills", body);
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Post_AccessCardDuplicateUidsInPayload_Returns400()
  {
    HttpClient client = Client(Roles.Receptionist);
    object body = WalkInBodyWithAccessCards(new[]
    {
      new { uid = 7UL, deposit = 0m, validUntil = "2026-08-15", note = (string?)null },
      new { uid = 7UL, deposit = 0m, validUntil = "2026-08-15", note = (string?)null },
    });

    HttpResponseMessage response = await client.PostAsJsonAsync("bills", body);
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  private static object WalkInBodyWithNewVehicles(object newVehicles) => new
  {
    reservationId = (Guid?)null,
    checkInAt = "2026-04-22",
    checkOutAt = "2026-04-23",
    payer = new { name = "John", surname = "Doe", address = MinimalAddress() },
    legalEntity = new { name = "Acme", cin = "123", tin = "CZ123", address = MinimalAddress() },
    paymentType = PaymentType.Cash,
    languageId = Guid.NewGuid(),
    items = new[]
    {
      new { serviceId = (Guid?)null, quantity = 1u, unitPrice = 300m, vatRatePercentage = 21m, recapSingleQuantity = 0u, recapDayQuantity = 0u },
    },
    linkedInvoiceIds = Array.Empty<Guid>(),
    existingGuests = Array.Empty<object>(),
    reservationSpotItemIds = Array.Empty<Guid>(),
    accessCards = Array.Empty<object>(),
    newVehicles,
    existingVehicleIds = Array.Empty<Guid>(),
    newGuests = new[]
    {
      new
      {
        firstName = "Walk",
        lastName = "In",
        nationalityId = Guid.NewGuid(),
        dateOfBirth = "1990-01-01",
        documentType = (int)DocumentType.Passport,
        documentNumber = "D1",
        address = MinimalAddress(),
        reasonOfStay = "Holiday",
        stayFrom = "2026-04-22",
        stayTo = "2026-04-23",
        email = (string?)null,
        phoneNumber = (string?)null,
        supplementaryDocumentNumber = (string?)null,
        documentIssuerCountryCode = (string?)null,
        visaNumber = (string?)null,
        note = (string?)null,
        paysRecreationFee = true,
      },
    },
  };

  private static object WalkInBodyWithAccessCards(object accessCards) => new
  {
    reservationId = (Guid?)null,
    checkInAt = "2026-04-22",
    checkOutAt = "2026-04-23",
    payer = new { name = "John", surname = "Doe", address = MinimalAddress() },
    legalEntity = new { name = "Acme", cin = "123", tin = "CZ123", address = MinimalAddress() },
    paymentType = PaymentType.Cash,
    languageId = Guid.NewGuid(),
    items = new[]
    {
      new { serviceId = (Guid?)null, quantity = 1u, unitPrice = 300m, vatRatePercentage = 21m, recapSingleQuantity = 0u, recapDayQuantity = 0u },
    },
    linkedInvoiceIds = Array.Empty<Guid>(),
    existingGuests = Array.Empty<object>(),
    reservationSpotItemIds = Array.Empty<Guid>(),
    accessCards,
    newVehicles = Array.Empty<object>(),
    existingVehicleIds = Array.Empty<Guid>(),
    newGuests = new[]
    {
      new
      {
        firstName = "Walk",
        lastName = "In",
        nationalityId = Guid.NewGuid(),
        dateOfBirth = "1990-01-01",
        documentType = (int)DocumentType.Passport,
        documentNumber = "D1",
        address = MinimalAddress(),
        reasonOfStay = "Holiday",
        stayFrom = "2026-04-22",
        stayTo = "2026-04-23",
        email = (string?)null,
        phoneNumber = (string?)null,
        supplementaryDocumentNumber = (string?)null,
        documentIssuerCountryCode = (string?)null,
        visaNumber = (string?)null,
        note = (string?)null,
        paysRecreationFee = true,
      },
    },
  };

  [Fact]
  public async Task Post_AccessCardUidAlreadyKnown_Returns201_AndOverwritesExistingCard()
  {
    HttpClient client = Client(Roles.Receptionist);

    var existingCardId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.AccessCards.Add(new Domain.Operations.AccessCards.AccessCard
      {
        Id = existingCardId,
        Uid = 42UL,
        BillId = null,
        Deposit = 0m,
        IssuedAtUtc = DateTime.UtcNow,
        Note = null,
      });
      await db.SaveChangesAsync();
    });

    object body = WalkInBodyWithAccessCards(new[]
    {
      new { uid = 42UL, deposit = 75m, validUntil = "2026-08-15", note = "transferred" },
    });

    HttpResponseMessage response = await client.PostAsJsonAsync("bills", body);
    response.StatusCode.ShouldBe(
      HttpStatusCode.Created,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    await _factory.WithDbAsync(async db =>
    {
      // Same row, overwritten in place - not duplicated.
      (await db.AccessCards.CountAsync(c => c.Uid == 42UL)).ShouldBe(1);
      Domain.Operations.AccessCards.AccessCard reloaded =
        (await db.AccessCards.FindAsync(existingCardId))!;
      reloaded.BillId.ShouldNotBeNull();
      reloaded.Deposit.ShouldBe(75m);
      reloaded.Note.ShouldBe("transferred");
    });
  }

  [Fact]
  public async Task Post_WalkInBillWithTwoAccessCards_Returns201_AndPersistsCardsLinkedToBill()
  {
    HttpClient client = Client(Roles.Receptionist);
    object body = WalkInBodyWithAccessCards(new[]
    {
      new { uid = 1001UL, deposit = 100m, validUntil = "2026-08-15", note = (string?)"card A" },
      new { uid = 1002UL, deposit = 200m, validUntil = "2026-08-15", note = (string?)null },
    });

    HttpResponseMessage response = await client.PostAsJsonAsync("bills", body);
    string error = _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception";
    response.StatusCode.ShouldBe(HttpStatusCode.Created, error);

    Dictionary<string, object>? created =
      await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
    Guid billId = ((System.Text.Json.JsonElement)created!["billId"]).GetGuid();

    await _factory.WithDbAsync(async db =>
    {
      var cards = (await db.AccessCards
        .Where(c => c.BillId == billId)
        .ToListAsync())
        .OrderBy(c => c.Uid)
        .ToList();
      cards.Count.ShouldBe(2);
      cards[0].Uid.ShouldBe(1001UL);
      cards[0].Deposit.ShouldBe(100m);
      cards[0].Note.ShouldBe("card A");
      cards[1].Uid.ShouldBe(1002UL);
      cards[1].Deposit.ShouldBe(200m);
      cards[1].Note.ShouldBeNull();
    });

    await _factory.GateClient.Received(1).PutCardAsync(1001UL, Arg.Any<GateCardPayload>(), Arg.Any<CancellationToken>());
    await _factory.GateClient.Received(1).PutCardAsync(1002UL, Arg.Any<GateCardPayload>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Post_NewVehicleWithEmptyRegistrationNumber_Returns400()
  {
    HttpClient client = Client(Roles.Receptionist);
    object body = WalkInBodyWithNewVehicles(new[]
    {
      new { registrationNumber = "", serviceId = Guid.NewGuid() },
    });

    HttpResponseMessage response = await client.PostAsJsonAsync("bills", body);
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Post_NewVehicleWithRegistrationOver20Chars_Returns400()
  {
    HttpClient client = Client(Roles.Receptionist);
    object body = WalkInBodyWithNewVehicles(new[]
    {
      new { registrationNumber = new string('X', 21), serviceId = Guid.NewGuid() },
    });

    HttpResponseMessage response = await client.PostAsJsonAsync("bills", body);
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Post_NewVehicleWithEmptyServiceId_Returns400()
  {
    HttpClient client = Client(Roles.Receptionist);
    object body = WalkInBodyWithNewVehicles(new[]
    {
      new { registrationNumber = "AB1234", serviceId = Guid.Empty },
    });

    HttpResponseMessage response = await client.PostAsJsonAsync("bills", body);
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Post_WalkInBillWithTwoNewVehicles_Returns201_AndPersistsVehiclesLinkedToBill()
  {
    HttpClient client = Client(Roles.Receptionist);
    var serviceA = Guid.NewGuid();
    var serviceB = Guid.NewGuid();
    object body = WalkInBodyWithNewVehicles(new[]
    {
      new { registrationNumber = "ABC123", serviceId = serviceA },
      new { registrationNumber = "DEF456", serviceId = serviceB },
    });

    HttpResponseMessage response = await client.PostAsJsonAsync("bills", body);
    string error = _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception";
    response.StatusCode.ShouldBe(HttpStatusCode.Created, error);

    Dictionary<string, object>? created =
      await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
    Guid billId = ((System.Text.Json.JsonElement)created!["billId"]).GetGuid();

    await _factory.WithDbAsync(async db =>
    {
      var byPlate = (await db.Vehicles
        .Where(v => v.BillId == billId)
        .ToListAsync())
        .OrderBy(v => v.RegistrationNumber, StringComparer.Ordinal)
        .ToList();
      byPlate.Count.ShouldBe(2);
      byPlate[0].RegistrationNumber.ShouldBe("ABC123");
      byPlate[0].ServiceId.ShouldBe(serviceA);
      byPlate[0].ReservationId.ShouldBeNull();
      byPlate[0].BillId.ShouldBe(billId);
      byPlate[1].RegistrationNumber.ShouldBe("DEF456");
      byPlate[1].ServiceId.ShouldBe(serviceB);
      byPlate[1].ReservationId.ShouldBeNull();
      byPlate[1].BillId.ShouldBe(billId);
    });
  }

  private static object WalkInBodyWithExistingVehicleIds(IEnumerable<Guid> existingVehicleIds) => new
  {
    reservationId = (Guid?)null,
    checkInAt = "2026-04-22",
    checkOutAt = "2026-04-23",
    payer = new { name = "John", surname = "Doe", address = MinimalAddress() },
    legalEntity = new { name = "Acme", cin = "123", tin = "CZ123", address = MinimalAddress() },
    paymentType = PaymentType.Cash,
    languageId = Guid.NewGuid(),
    items = new[]
    {
      new { serviceId = (Guid?)null, quantity = 1u, unitPrice = 300m, vatRatePercentage = 21m, recapSingleQuantity = 0u, recapDayQuantity = 0u },
    },
    linkedInvoiceIds = Array.Empty<Guid>(),
    existingGuests = Array.Empty<object>(),
    reservationSpotItemIds = Array.Empty<Guid>(),
    accessCards = Array.Empty<object>(),
    newVehicles = Array.Empty<object>(),
    existingVehicleIds,
    newGuests = new[]
    {
      new
      {
        firstName = "Walk", lastName = "In",
        nationalityId = Guid.NewGuid(),
        dateOfBirth = "1990-01-01",
        documentType = (int)DocumentType.Passport,
        documentNumber = "D1",
        address = MinimalAddress(),
        reasonOfStay = "Holiday",
        stayFrom = "2026-04-22", stayTo = "2026-04-23",
        email = (string?)null, phoneNumber = (string?)null,
        supplementaryDocumentNumber = (string?)null,
        documentIssuerCountryCode = (string?)null,
        visaNumber = (string?)null, note = (string?)null,
        paysRecreationFee = true,
      },
    },
  };

  [Fact]
  public async Task Post_BillWithExistingVehicle_Returns201_AndSetsBillIdOnVehicle()
  {
    HttpClient client = Client(Roles.Receptionist);

    var reservationId = Guid.NewGuid();
    var serviceId = Guid.NewGuid();
    var vehicleId = Guid.NewGuid();
    var guestId = Guid.NewGuid();

    await _factory.WithDbAsync(async db =>
    {
      db.Reservations.Add(new Domain.Reservations.Reservations.Reservation
      {
        Id = reservationId,
        Number = "R-VEH",
        Period = new Domain.Common.DateRange(new DateOnly(2026, 4, 20), new DateOnly(2026, 4, 22)),
        ReservationMaker = new Domain.Reservations.ReservationMakers.ReservationMaker(
          "M", "M", "m@example.com", "+420000000000"),
        State = Domain.Reservations.ReservationStates.ReservationState.Confirmed,
        CreatedAtUtc = DateTime.UtcNow,
        Secret = Guid.NewGuid().ToString("N"),
      });
      db.Guests.Add(new Domain.Reservations.Guests.Guest
      {
        Id = guestId,
        ReservationId = reservationId,
        FirstName = "F",
        LastName = "L",
        NationalityId = Guid.NewGuid(),
        DateOfBirth = new DateOnly(1990, 1, 1),
        DocumentType = Domain.Reservations.Guests.DocumentType.IdCard,
        DocumentNumber = "D1",
        Address = new Domain.Common.Address(Guid.NewGuid(), "Prague", "10000", "Main", "1"),
        ReasonOfStay = "Holiday",
      });
      db.Vehicles.Add(new Domain.Reservations.Vehicles.Vehicle
      {
        Id = vehicleId,
        ReservationId = reservationId,
        BillId = null,
        ServiceId = serviceId,
        RegistrationNumber = "EXISTING1",
      });
      await db.SaveChangesAsync();
    });

    var body = new
    {
      reservationId = (Guid?)reservationId,
      checkInAt = "2026-04-20",
      checkOutAt = "2026-04-22",
      payer = new { name = "John", surname = "Doe", address = MinimalAddress() },
      legalEntity = new { name = "Acme", cin = "123", tin = "CZ123", address = MinimalAddress() },
      paymentType = PaymentType.Card,
      languageId = Guid.NewGuid(),
      items = new[]
      {
        new { serviceId = (Guid?)null, quantity = 1u, unitPrice = 500m, vatRatePercentage = 21m, recapSingleQuantity = 0u, recapDayQuantity = 0u },
      },
      linkedInvoiceIds = Array.Empty<Guid>(),
      existingGuests = new[] { new { guestId, paysRecreationFee = true } },
      reservationSpotItemIds = Array.Empty<Guid>(),
      accessCards = Array.Empty<object>(),
      newVehicles = Array.Empty<object>(),
      existingVehicleIds = new[] { vehicleId },
      newGuests = Array.Empty<object>(),
    };

    HttpResponseMessage response = await client.PostAsJsonAsync("bills", body);
    string error = _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception";
    response.StatusCode.ShouldBe(HttpStatusCode.Created, error);

    Dictionary<string, object>? created =
      await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
    Guid billId = ((System.Text.Json.JsonElement)created!["billId"]).GetGuid();

    await _factory.WithDbAsync(async db =>
    {
      Domain.Reservations.Vehicles.Vehicle v = await db.Vehicles.FirstAsync(x => x.Id == vehicleId);
      v.BillId.ShouldBe(billId);
      v.ReservationId.ShouldBe(reservationId);
      v.ServiceId.ShouldBe(serviceId);
      v.RegistrationNumber.ShouldBe("EXISTING1");
    });
  }

  [Fact]
  public async Task Post_ExistingVehicleNotFound_Returns404()
  {
    HttpClient client = Client(Roles.Receptionist);
    var missing = Guid.NewGuid();

    object body = WalkInBodyWithExistingVehicleIds(new[] { missing });

    HttpResponseMessage response = await client.PostAsJsonAsync("bills", body);
    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task Post_ExistingVehicleAlreadyLinkedToBill_Returns409()
  {
    HttpClient client = Client(Roles.Receptionist);

    var vehicleId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.Vehicles.Add(new Domain.Reservations.Vehicles.Vehicle
      {
        Id = vehicleId,
        ReservationId = null,
        BillId = Guid.NewGuid(),  // already linked to some other bill
        ServiceId = null,
        RegistrationNumber = "LINKED1",
      });
      await db.SaveChangesAsync();
    });

    object body = WalkInBodyWithExistingVehicleIds(new[] { vehicleId });

    HttpResponseMessage response = await client.PostAsJsonAsync("bills", body);
    response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
  }

  [Fact]
  public async Task Post_ExistingVehicleFromAnotherReservation_Returns400()
  {
    HttpClient client = Client(Roles.Receptionist);

    var foreignReservationId = Guid.NewGuid();
    var vehicleId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.Vehicles.Add(new Domain.Reservations.Vehicles.Vehicle
      {
        Id = vehicleId,
        ReservationId = foreignReservationId,
        BillId = null,
        ServiceId = null,
        RegistrationNumber = "OTHER1",
      });
      await db.SaveChangesAsync();
    });

    object body = WalkInBodyWithExistingVehicleIds(new[] { vehicleId });

    HttpResponseMessage response = await client.PostAsJsonAsync("bills", body);
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Post_DuplicateExistingVehicleIds_Returns400()
  {
    HttpClient client = Client(Roles.Receptionist);
    var duplicateId = Guid.NewGuid();
    object body = new
    {
      reservationId = (Guid?)null,
      checkInAt = "2026-04-22",
      checkOutAt = "2026-04-23",
      payer = new { name = "John", surname = "Doe", address = MinimalAddress() },
      legalEntity = new { name = "Acme", cin = "123", tin = "CZ123", address = MinimalAddress() },
      paymentType = PaymentType.Cash,
      languageId = Guid.NewGuid(),
      items = new[]
      {
        new { serviceId = (Guid?)null, quantity = 1u, unitPrice = 300m, vatRatePercentage = 21m, recapSingleQuantity = 0u, recapDayQuantity = 0u },
      },
      linkedInvoiceIds = Array.Empty<Guid>(),
      existingGuests = Array.Empty<object>(),
      reservationSpotItemIds = Array.Empty<Guid>(),
      accessCards = Array.Empty<object>(),
      newVehicles = Array.Empty<object>(),
      existingVehicleIds = new[] { duplicateId, duplicateId },
      newGuests = new[]
      {
        new
        {
          firstName = "Walk", lastName = "In",
          nationalityId = Guid.NewGuid(),
          dateOfBirth = "1990-01-01",
          documentType = (int)DocumentType.Passport,
          documentNumber = "D1",
          address = MinimalAddress(),
          reasonOfStay = "Holiday",
          stayFrom = "2026-04-22", stayTo = "2026-04-23",
          email = (string?)null, phoneNumber = (string?)null,
          supplementaryDocumentNumber = (string?)null,
          documentIssuerCountryCode = (string?)null,
          visaNumber = (string?)null, note = (string?)null,
          paysRecreationFee = true,
        },
      },
    };

    HttpResponseMessage response = await client.PostAsJsonAsync("bills", body);
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }
}
