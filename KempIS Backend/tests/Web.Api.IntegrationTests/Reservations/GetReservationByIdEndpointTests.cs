using Application.Abstractions.Authentication;
using Application.Reservations.Queries.GetReservationById;
using Domain.Common;
using Domain.Finance.Bills;
using Domain.Finance.Invoices;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Domain.Operations.AccessCards;
using Domain.Reservations.Guests;
using Domain.Reservations.Meals;
using Domain.Reservations.Nationalities;
using Domain.Reservations.ReservationServiceItems;
using Domain.Reservations.ReservationSpotItems;
using Domain.Reservations.ReservationStates;
using Domain.Reservations.Vehicles;
using Domain.Services.Services;
using Domain.Services.ServiceTypes;
using Domain.Services.VatRates;
using TestUtilities.Builders;
using Web.Api.IntegrationTests.Infrastructure;
using DomainReservation = Domain.Reservations.Reservations.Reservation;

namespace Web.Api.IntegrationTests.Reservations;

public sealed class GetReservationByIdEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public GetReservationByIdEndpointTests(ApiFactory factory) => _factory = factory;

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

  [Fact]
  public async Task Get_Anonymous_Returns401()
  {
    HttpResponseMessage response = await Client().GetAsync(
      new Uri($"reservations/{Guid.NewGuid()}", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Get_AsCleaningStaff_Returns403()
  {
    HttpResponseMessage response = await Client(Roles.CleaningStaff).GetAsync(
      new Uri($"reservations/{Guid.NewGuid()}", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task Get_UnknownId_Returns404()
  {
    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri($"reservations/{Guid.NewGuid()}", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task Get_ExistingReservation_ReturnsHeaderWithEmptyCollections()
  {
    DomainReservation reservation = new ReservationBuilder()
      .WithNumber("R-2026/0042")
      .For(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5))
      .InState(ReservationState.Confirmed)
      .MadeBy("Anna", "Smith", "anna@example.com", "+420111222333")
      .WithNote("test note")
      .Build();

    await _factory.WithDbAsync(async db =>
    {
      db.Reservations.Add(reservation);
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri($"reservations/{reservation.Id}", UriKind.Relative));

    response.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    ReservationDetailResponse? body = await response.Content.ReadFromJsonAsync<ReservationDetailResponse>();
    body.ShouldNotBeNull();
    body.Id.ShouldBe(reservation.Id);
    body.Number.ShouldBe("R-2026/0042");
    body.State.ShouldBe(ReservationState.Confirmed);
    body.From.ShouldBe(new DateOnly(2026, 6, 1));
    body.To.ShouldBe(new DateOnly(2026, 6, 5));
    body.ReservationMakerName.ShouldBe("Anna");
    body.ReservationMakerSurname.ShouldBe("Smith");
    body.ReservationMakerEmail.ShouldBe("anna@example.com");
    body.ReservationMakerPhone.ShouldBe("+420111222333");
    body.Note.ShouldBe("test note");
    body.Guests.ShouldBeEmpty();
    body.SpotItems.ShouldBeEmpty();
    body.ServiceItems.ShouldBeEmpty();
    body.Vehicles.ShouldBeEmpty();
    body.Meals.ShouldBeEmpty();
    body.Invoices.ShouldBeEmpty();
    body.Bills.ShouldBeEmpty();
    body.AccessCards.ShouldBeEmpty();
  }

  [Fact]
  public async Task Get_ReservationWithInlineVehicles_ProjectsThemOrderedByRegistration()
  {
    DomainReservation reservation = new ReservationBuilder()
      .For(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5))
      .Build();

    var firstVehicleId = Guid.NewGuid();
    var secondVehicleId = Guid.NewGuid();
    var billId = Guid.NewGuid();
    var serviceId = Guid.NewGuid();
    var otherReservationId = Guid.NewGuid();

    await _factory.WithDbAsync(async db =>
    {
      db.Reservations.Add(reservation);

      db.Vehicles.Add(new Vehicle
      {
        Id = firstVehicleId,
        ReservationId = reservation.Id,
        BillId = null,
        ServiceId = null,
        RegistrationNumber = "5AB 1234",
      });

      db.Vehicles.Add(new Vehicle
      {
        Id = secondVehicleId,
        ReservationId = reservation.Id,
        BillId = billId,
        ServiceId = serviceId,
        RegistrationNumber = "1XY 9999",
      });

      // Vehicle on a different reservation must not bleed in.
      db.Vehicles.Add(new Vehicle
      {
        Id = Guid.NewGuid(),
        ReservationId = otherReservationId,
        RegistrationNumber = "ZZZ 0000",
      });

      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri($"reservations/{reservation.Id}", UriKind.Relative));

    response.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    ReservationDetailResponse? body = await response.Content.ReadFromJsonAsync<ReservationDetailResponse>();
    body.ShouldNotBeNull();

    body.Vehicles.Count.ShouldBe(2);
    body.Vehicles.Select(v => v.RegistrationNumber).ShouldBe(["1XY 9999", "5AB 1234"]);

    ReservationDetailVehicle billed = body.Vehicles.Single(v => v.Id == secondVehicleId);
    billed.BillId.ShouldBe(billId);
    billed.ServiceId.ShouldBe(serviceId);
    billed.RegistrationNumber.ShouldBe("1XY 9999");

    ReservationDetailVehicle plain = body.Vehicles.Single(v => v.Id == firstVehicleId);
    plain.BillId.ShouldBeNull();
    plain.ServiceId.ShouldBeNull();
    plain.RegistrationNumber.ShouldBe("5AB 1234");
  }

  [Fact]
  public async Task Get_ExistingReservation_AggregatesAllRelatedCollections()
  {
    DomainReservation reservation = new ReservationBuilder()
      .For(new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 14))
      .Build();

    var spotGroupId = Guid.NewGuid();
    var spotId = Guid.NewGuid();
    Guid nationalityId = Guid.Empty;
    var guestId = Guid.NewGuid();
    var spotItemId = Guid.NewGuid();
    var serviceItemId = Guid.NewGuid();
    var serviceId = Guid.NewGuid();
    var directBillId = Guid.NewGuid();
    var indirectBillId = Guid.NewGuid();
    var unrelatedBillId = Guid.NewGuid();
    var invoiceId = Guid.NewGuid();
    var accessCardId = Guid.NewGuid();
    var languageId = Guid.NewGuid();

    await _factory.WithDbAsync(async db =>
    {
      db.Reservations.Add(reservation);

      // ResetAllAsync re-seeds reference data, so any seeded nationality will do.
      Nationality seedNationality = await db.Nationalities.AsNoTracking().FirstAsync();
      nationalityId = seedNationality.Id;

      db.Guests.Add(new Guest
      {
        Id = guestId,
        ReservationId = reservation.Id,
        FirstName = "Petra",
        LastName = "Vesela",
        NationalityId = nationalityId,
        DateOfBirth = new DateOnly(1990, 3, 15),
        DocumentType = DocumentType.IdCard,
        DocumentNumber = "ID12345",
        Address = new Address(Guid.NewGuid(), "Brno", "60200", "Hlavni", "10"),
        ReasonOfStay = "Tourism",
        StayDateRange = new DateRange(reservation.Period.From, reservation.Period.To),
        CheckInAt = new DateTime(2026, 7, 10, 14, 0, 0, DateTimeKind.Utc),
      });

      db.ReservationSpotItems.Add(new ReservationSpotItem
      {
        Id = spotItemId,
        ReservationId = reservation.Id,
        SpotGroupId = spotGroupId,
        SpotId = spotId,
        HasReturnedKeys = true,
      });

      ServiceType serviceType = new()
      {
        Id = Guid.NewGuid(),
        Name = "Test type",
        IsActive = true,
      };
      db.ServiceTypes.Add(serviceType);

      VatRate vat = new()
      {
        Id = Guid.NewGuid(),
        Name = "Zero",
        Rate = 0m,
        IsActive = true,
      };
      db.VatRates.Add(vat);

      db.Services.Add(new Service
      {
        Id = serviceId,
        Name = "Test service",
        ServiceGroup = ServiceGroup.Others,
        ServiceTypeId = serviceType.Id,
        VatRateId = vat.Id,
        BasePrice = 0m,
        IsActive = true,
      });

      db.ReservationServiceItems.Add(new ReservationServiceItem
      {
        Id = serviceItemId,
        ReservationId = reservation.Id,
        ServiceId = serviceId,
        Quantity = 3u,
        RecapSingleQuantity = 1u,
        RecapDayQuantity = 4u,
      });

      db.Meals.Add(new Meal
      {
        ReservationId = reservation.Id,
        Date = new DateOnly(2026, 7, 11),
        Breakfast = MealAmount.Empty with { Normal = 2 },
        Lunch = MealAmount.Empty with { Normal = 1 },
        LunchPackage = MealAmount.Empty,
        Dinner = MealAmount.Empty with { Normal = 2 },
      });

      static Payer NewPayer() => new()
      {
        Name = "Anna",
        Surname = "Buyer",
        Address = new Address(Guid.NewGuid(), "Praha", "10000", "Hlavni", "1"),
      };
      static LegalEntity NewLegalEntity() => new()
      {
        Name = "Test s.r.o.",
        Address = new Address(Guid.NewGuid(), "Praha", "10000", "Hlavni", "1"),
        Cin = "11111111",
        Tin = "CZ11111111",
      };

      db.Bills.Add(new Bill
      {
        Id = directBillId,
        Number = "B-DIRECT-001",
        ReservationId = reservation.Id,
        LanguageIdGuid = languageId,
        IssuedAtUtc = new DateTime(2026, 7, 14, 10, 0, 0, DateTimeKind.Utc),
        CheckInAt = reservation.Period.From,
        CheckOutAt = reservation.Period.To,
        Payer = NewPayer(),
        LegalEntity = NewLegalEntity(),
        Payment = new Payment(PaymentType.Cash, 1500m),
      });

      db.Bills.Add(new Bill
      {
        Id = indirectBillId,
        Number = "B-INDIRECT-001",
        ReservationId = null,
        LanguageIdGuid = languageId,
        IssuedAtUtc = new DateTime(2026, 7, 15, 9, 0, 0, DateTimeKind.Utc),
        CheckInAt = reservation.Period.From,
        CheckOutAt = reservation.Period.To,
        Payer = NewPayer(),
        LegalEntity = NewLegalEntity(),
        Payment = new Payment(PaymentType.Card, 200m),
      });

      db.Bills.Add(new Bill
      {
        Id = unrelatedBillId,
        Number = "B-OTHER-001",
        ReservationId = Guid.NewGuid(),
        LanguageIdGuid = languageId,
        IssuedAtUtc = new DateTime(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc),
        CheckInAt = reservation.Period.From,
        CheckOutAt = reservation.Period.To,
        Payer = NewPayer(),
        LegalEntity = NewLegalEntity(),
        Payment = new Payment(PaymentType.Cash, 999m),
      });

      db.Invoices.Add(new Invoice
      {
        Id = invoiceId,
        ReservationId = reservation.Id,
        Number = "INV-2026-0001",
        Status = InvoiceStatus.Paid,
        IssuedAt = new DateOnly(2026, 7, 12),
        PaidAt = new DateOnly(2026, 7, 13),
        LinkedBillId = indirectBillId,
        Email = "seed@example.com",
        PhoneNumber = "+420000000000",
        Payer = NewPayer(),
      });

      db.AccessCards.Add(new AccessCard
      {
        Id = accessCardId,
        Uid = 4242UL,
        BillId = directBillId,
        Deposit = 100m,
        IssuedAtUtc = new DateTime(2026, 7, 10, 14, 5, 0, DateTimeKind.Utc),
      });

      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client(Roles.Manager).GetAsync(
      new Uri($"reservations/{reservation.Id}", UriKind.Relative));

    response.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    ReservationDetailResponse? body = await response.Content.ReadFromJsonAsync<ReservationDetailResponse>();
    body.ShouldNotBeNull();

    body.Guests.Count.ShouldBe(1);
    body.Guests[0].Id.ShouldBe(guestId);
    body.Guests[0].FirstName.ShouldBe("Petra");
    body.Guests[0].StayFrom.ShouldBe(reservation.Period.From);
    body.Guests[0].StayTo.ShouldBe(reservation.Period.To);
    body.Guests[0].CheckInAt.ShouldNotBeNull();

    body.SpotItems.Count.ShouldBe(1);
    body.SpotItems[0].Id.ShouldBe(spotItemId);
    body.SpotItems[0].SpotGroupId.ShouldBe(spotGroupId);
    body.SpotItems[0].SpotId.ShouldBe(spotId);
    body.SpotItems[0].HasReturnedKeys.ShouldBeTrue();

    body.ServiceItems.Count.ShouldBe(1);
    body.ServiceItems[0].Id.ShouldBe(serviceItemId);
    body.ServiceItems[0].ServiceId.ShouldBe(serviceId);
    body.ServiceItems[0].Quantity.ShouldBe(3u);

    body.Meals.Count.ShouldBe(1);
    body.Meals[0].Date.ShouldBe(new DateOnly(2026, 7, 11));
    body.Meals[0].Breakfast.Normal.ShouldBe(2u);
    body.Meals[0].Lunch.Normal.ShouldBe(1u);
    body.Meals[0].LunchPackage.Normal.ShouldBe(0u);
    body.Meals[0].Dinner.Normal.ShouldBe(2u);

    body.Invoices.Count.ShouldBe(1);
    body.Invoices[0].Id.ShouldBe(invoiceId);
    body.Invoices[0].Status.ShouldBe(InvoiceStatus.Paid);
    body.Invoices[0].LinkedBillId.ShouldBe(indirectBillId);

    body.Bills.Select(b => b.Id).ShouldBe([indirectBillId, directBillId], ignoreOrder: true);
    body.Bills.ShouldNotContain(b => b.Id == unrelatedBillId);

    body.AccessCards.Count.ShouldBe(1);
    body.AccessCards[0].Id.ShouldBe(accessCardId);
    body.AccessCards[0].Uid.ShouldBe(4242UL);
    body.AccessCards[0].Deposit.ShouldBe(100m);
  }

  [Fact]
  public async Task GetReservationById_ProjectsHasGivenKey_OnSpotItems()
  {
    DomainReservation reservation = new ReservationBuilder()
      .For(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 5))
      .Build();

    var spotGroupId = Guid.NewGuid();
    var spotId = Guid.NewGuid();
    var spotItemId = Guid.NewGuid();

    await _factory.WithDbAsync(async db =>
    {
      db.Reservations.Add(reservation);

      db.ReservationSpotItems.Add(new ReservationSpotItem
      {
        Id = spotItemId,
        ReservationId = reservation.Id,
        SpotGroupId = spotGroupId,
        SpotId = spotId,
        HasGivenKey = true,
        HasReturnedKeys = false,
      });

      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri($"reservations/{reservation.Id}", UriKind.Relative));

    response.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    ReservationDetailResponse? body = await response.Content.ReadFromJsonAsync<ReservationDetailResponse>();
    body.ShouldNotBeNull();

    body.SpotItems.Count.ShouldBe(1);
    body.SpotItems[0].Id.ShouldBe(spotItemId);
    body.SpotItems[0].SpotGroupId.ShouldBe(spotGroupId);
    body.SpotItems[0].SpotId.ShouldBe(spotId);
    body.SpotItems[0].HasGivenKey.ShouldBeTrue();
    body.SpotItems[0].HasReturnedKeys.ShouldBeFalse();
  }
}
