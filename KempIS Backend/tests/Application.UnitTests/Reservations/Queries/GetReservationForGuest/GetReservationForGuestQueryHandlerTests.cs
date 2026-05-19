using Application.Reservations.Queries.GetReservationForGuest;
using Domain.Common;
using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Domain.Reservations;
using Domain.Reservations.Meals;
using Domain.Reservations.ReservationStates;
using Domain.Services.ServiceTexts;
using SharedKernel;
using TestUtilities.Builders;
using DomainReservation = Domain.Reservations.Reservations.Reservation;
using DomainReservationSpotItem = Domain.Reservations.ReservationSpotItems.ReservationSpotItem;

namespace Application.UnitTests.Reservations.Queries.GetReservationForGuest;

public sealed class GetReservationForGuestQueryHandlerTests : HandlerTestBase
{
  private GetReservationForGuestQueryHandler CreateSut() => new(Db);

  private async Task<DomainReservation> SeedReservation(string secret = "guest-secret")
  {
    DomainReservation r = new ReservationBuilder()
      .InState(ReservationState.Created)
      .For(new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 12))
      .MadeBy("Jan", "Novak", "jan@example.com", "+420111000")
      .WithNote("please prepare early check-in")
      .WithSecret(secret)
      .Build();
    Db.Reservations.Add(r);
    await Db.SaveChangesAsync();
    return r;
  }

  private static Address Addr() => new(Guid.NewGuid(), "Prague", "10000", "Main", "1");

  [Fact]
  public async Task Handle_MatchingSecret_ReturnsBaseProjectionWithoutContactDetails()
  {
    DomainReservation r = await SeedReservation(secret: "correct");
    Db.ReservationSpotItems.Add(new DomainReservationSpotItem
    {
      Id = Guid.NewGuid(),
      ReservationId = r.Id,
      SpotGroupId = Guid.NewGuid(),
      SpotId = null,
    });
    await Db.SaveChangesAsync();

    Result<ReservationForGuestResponse> result = await CreateSut().Handle(
        new GetReservationForGuestQuery(r.Id, "correct"), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    ReservationForGuestResponse dto = result.Value;
    dto.Id.ShouldBe(r.Id);
    dto.State.ShouldBe("Created");
    dto.From.ShouldBe(new DateOnly(2026, 7, 10));
    dto.To.ShouldBe(new DateOnly(2026, 7, 12));
    dto.Name.ShouldBe("Jan");
    dto.Surname.ShouldBe("Novak");
    dto.Note.ShouldBe("please prepare early check-in");
    dto.SpotItems.Count.ShouldBe(1);
    dto.Meals.ShouldBeEmpty();
    dto.Bills.ShouldBeEmpty();
    typeof(ReservationForGuestResponse).GetProperty("Email").ShouldBeNull();
    typeof(ReservationForGuestResponse).GetProperty("Phone").ShouldBeNull();
  }

  [Fact]
  public async Task Handle_ReservationMissing_ReturnsNotFound()
  {
    var missing = Guid.NewGuid();

    Result<ReservationForGuestResponse> result = await CreateSut().Handle(
        new GetReservationForGuestQuery(missing, "any"), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.NotFound(missing));
  }

  [Fact]
  public async Task Handle_SecretMismatch_ReturnsSecretInvalid()
  {
    DomainReservation r = await SeedReservation(secret: "real");

    Result<ReservationForGuestResponse> result = await CreateSut().Handle(
        new GetReservationForGuestQuery(r.Id, "wrong"), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.SecretInvalid);
  }

  [Fact]
  public async Task Handle_SecretCaseSensitive_CaseMismatchIsRejected()
  {
    DomainReservation r = await SeedReservation(secret: "CaseSensitive");

    Result<ReservationForGuestResponse> result = await CreateSut().Handle(
        new GetReservationForGuestQuery(r.Id, "casesensitive"), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.SecretInvalid);
  }

  [Fact]
  public async Task Handle_SpotItem_IncludesSpotGroupAndSpotNamesAndAllServiceTexts()
  {
    DomainReservation r = await SeedReservation(secret: "correct");

    var serviceId = Guid.NewGuid();
    var groupId = Guid.NewGuid();
    var spotId = Guid.NewGuid();
    var langCs = Guid.NewGuid();
    var langEn = Guid.NewGuid();

    var siblingSpotId = Guid.NewGuid();
    Db.SpotGroups.Add(new SpotGroupBuilder()
      .WithId(groupId).WithServiceId(serviceId).WithName("Cottage A").Build());
    Db.Spots.Add(new SpotBuilder()
      .WithId(spotId).InGroup(groupId).WithName("A1").Build());
    Db.Spots.Add(new SpotBuilder()
      .WithId(siblingSpotId).InGroup(groupId).WithName("A2").Build());
    Db.Spots.Add(new SpotBuilder()
      .InGroup(groupId).WithName("A3-retired").Inactive().Build());
    Db.ServiceTexts.Add(new ServiceText
    {
      Id = Guid.NewGuid(),
      ServiceId = serviceId,
      LanguageId = langCs,
      PrintText = "Chata pro 4 osoby",
    });
    Db.ServiceTexts.Add(new ServiceText
    {
      Id = Guid.NewGuid(),
      ServiceId = serviceId,
      LanguageId = langEn,
      PrintText = "Cottage for 4 guests",
    });
    Db.ReservationSpotItems.Add(new DomainReservationSpotItem
    {
      Id = Guid.NewGuid(),
      ReservationId = r.Id,
      SpotGroupId = groupId,
      SpotId = spotId,
    });
    await Db.SaveChangesAsync();

    Result<ReservationForGuestResponse> result = await CreateSut().Handle(
        new GetReservationForGuestQuery(r.Id, "correct"), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    ReservationForGuestSpotItem item = result.Value.SpotItems.ShouldHaveSingleItem();
    item.SpotGroupId.ShouldBe(groupId);
    item.SpotGroupName.ShouldBe("Cottage A");
    item.SpotId.ShouldBe(spotId);
    item.SpotName.ShouldBe("A1");
    item.GroupSpots.Count.ShouldBe(2);
    item.GroupSpots.Select(s => s.Name).ShouldBe(["A1", "A2"]);
    item.ServiceTexts.Count.ShouldBe(2);
    item.ServiceTexts.ShouldContain(t => t.LanguageId == langCs && t.PrintText == "Chata pro 4 osoby");
    item.ServiceTexts.ShouldContain(t => t.LanguageId == langEn && t.PrintText == "Cottage for 4 guests");
  }

  [Fact]
  public async Task Handle_IncludesMealsForReservation_OrderedByDate()
  {
    DomainReservation r = await SeedReservation(secret: "correct");

    Db.Meals.Add(new Meal
    {
      ReservationId = r.Id,
      Date = new DateOnly(2026, 7, 11),
      Breakfast = MealAmount.Empty with { Normal = 2 },
      Lunch = MealAmount.Empty,
      LunchPackage = MealAmount.Empty with { Normal = 1 },
      Dinner = MealAmount.Empty with { Normal = 2 },
    });
    Db.Meals.Add(new Meal
    {
      ReservationId = r.Id,
      Date = new DateOnly(2026, 7, 10),
      Lunch = MealAmount.Empty with { Normal = 2 },
    });
    Db.Meals.Add(new Meal
    {
      ReservationId = Guid.NewGuid(),
      Date = new DateOnly(2026, 7, 10),
      Breakfast = MealAmount.Empty with { Normal = 9 },
      Lunch = MealAmount.Empty with { Normal = 9 },
      LunchPackage = MealAmount.Empty with { Normal = 9 },
      Dinner = MealAmount.Empty with { Normal = 9 },
    });
    await Db.SaveChangesAsync();

    Result<ReservationForGuestResponse> result = await CreateSut().Handle(
        new GetReservationForGuestQuery(r.Id, "correct"), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Meals.Count.ShouldBe(2);
    result.Value.Meals[0].Date.ShouldBe(new DateOnly(2026, 7, 10));
    result.Value.Meals[0].Lunch.Normal.ShouldBe(2u);
    result.Value.Meals[1].Date.ShouldBe(new DateOnly(2026, 7, 11));
    result.Value.Meals[1].Breakfast.Normal.ShouldBe(2u);
    result.Value.Meals[1].LunchPackage.Normal.ShouldBe(1u);
  }

  [Fact]
  public async Task Handle_IncludesBillsForReservation_OrderedByIssuedAtUtcDescending()
  {
    DomainReservation r = await SeedReservation(secret: "correct");

    Db.Bills.Add(new Bill
    {
      Id = Guid.NewGuid(),
      Number = "2026/0001",
      Kind = BillKind.Regular,
      ReservationId = r.Id,
      LanguageIdGuid = Guid.NewGuid(),
      IssuedAtUtc = new DateTime(2026, 7, 12, 10, 0, 0, DateTimeKind.Utc),
      CheckInAt = new DateOnly(2026, 7, 10),
      CheckOutAt = new DateOnly(2026, 7, 12),
      Payer = new Payer { Name = "John", Surname = "Doe", Address = Addr() },
      LegalEntity = new LegalEntity { Name = "Acme", Cin = "1", Tin = "1", Address = Addr() },
      Payment = new Payment(PaymentType.Card, 2500m),
    });
    Db.Bills.Add(new Bill
    {
      Id = Guid.NewGuid(),
      Number = "2026/0002",
      Kind = BillKind.Repair,
      ReservationId = r.Id,
      LanguageIdGuid = Guid.NewGuid(),
      IssuedAtUtc = new DateTime(2026, 7, 13, 9, 0, 0, DateTimeKind.Utc),
      CheckInAt = new DateOnly(2026, 7, 10),
      CheckOutAt = new DateOnly(2026, 7, 12),
      Payer = new Payer { Name = "John", Surname = "Doe", Address = Addr() },
      LegalEntity = new LegalEntity { Name = "Acme", Cin = "1", Tin = "1", Address = Addr() },
      Payment = new Payment(PaymentType.Cash, -500m),
    });
    Db.Bills.Add(new Bill
    {
      Id = Guid.NewGuid(),
      Number = "OTHER",
      Kind = BillKind.Regular,
      ReservationId = Guid.NewGuid(),
      LanguageIdGuid = Guid.NewGuid(),
      IssuedAtUtc = new DateTime(2026, 7, 14, 9, 0, 0, DateTimeKind.Utc),
      CheckInAt = new DateOnly(2026, 7, 1),
      CheckOutAt = new DateOnly(2026, 7, 2),
      Payer = new Payer { Name = "X", Surname = "Y", Address = Addr() },
      LegalEntity = new LegalEntity { Name = "Z", Cin = "1", Tin = "1", Address = Addr() },
      Payment = new Payment(PaymentType.Card, 100m),
    });
    await Db.SaveChangesAsync();

    Result<ReservationForGuestResponse> result = await CreateSut().Handle(
        new GetReservationForGuestQuery(r.Id, "correct"), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Bills.Count.ShouldBe(2);
    result.Value.Bills[0].Number.ShouldBe("2026/0002");
    result.Value.Bills[0].Kind.ShouldBe(BillKind.Repair);
    result.Value.Bills[0].Amount.ShouldBe(-500m);
    result.Value.Bills[1].Number.ShouldBe("2026/0001");
    result.Value.Bills[1].Kind.ShouldBe(BillKind.Regular);
    result.Value.Bills[1].Amount.ShouldBe(2500m);
  }
}
