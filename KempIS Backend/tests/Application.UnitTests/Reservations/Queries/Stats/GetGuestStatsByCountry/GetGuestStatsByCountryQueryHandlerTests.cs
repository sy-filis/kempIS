using Application.Reservations.Queries.Stats.GetGuestStatsByCountry;
using Domain.Common;
using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Domain.Reservations.Guests;
using Domain.Reservations.Nationalities;
using SharedKernel;

namespace Application.UnitTests.Reservations.Queries.Stats.GetGuestStatsByCountry;

public sealed class GetGuestStatsByCountryQueryHandlerTests : HandlerTestBase
{
  private static readonly DateOnly From = new(2026, 6, 1);
  private static readonly DateOnly To = new(2026, 8, 31);
  private static readonly string[] CzeDeuSvk = ["CZE", "DEU", "SVK"];

  private GetGuestStatsByCountryQueryHandler CreateSut() => new(Db);

  private static int _numericCounter;

  private static Nationality MakeNationality(Guid id, string alpha2, string alpha3, string name, string nameEn) =>
    new()
    {
      Id = id,
      Name = name,
      NameEn = nameEn,
      Alpha2 = alpha2,
      Alpha3 = alpha3,
      Numeric = (++_numericCounter).ToString("D3", System.Globalization.CultureInfo.InvariantCulture),
      VisaRequired = false,
      BiometricsRequired = false,
      IsEu = true,
      LanguageId = Guid.NewGuid(),
    };

  private static Bill MakeBill(Guid id, DateOnly checkIn, DateOnly checkOut) => new()
  {
    Id = id,
    Number = "B-" + id.ToString("N")[..6],
    ReservationId = Guid.NewGuid(),
    IssuedAtUtc = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc),
    CheckInAt = checkIn,
    CheckOutAt = checkOut,
    LanguageIdGuid = Guid.NewGuid(),
    Payer = new Payer
    {
      Name = "Jan",
      Surname = "Novak",
      Address = new Address(Guid.NewGuid(), "Prague", "10000", "Main", "1"),
    },
    LegalEntity = new LegalEntity
    {
      Name = "Acme",
      Address = new Address(Guid.NewGuid(), "Prague", "10000", "Main", "1"),
      Cin = "12345678",
      Tin = "CZ12345678",
    },
    Payment = new Payment(PaymentType.Cash, 0m),
  };

  private static Guest MakeGuest(Guid nationalityId, Guid billId) => new()
  {
    Id = Guid.NewGuid(),
    BillId = billId,
    ReservationId = Guid.NewGuid(),
    FirstName = "Anna",
    LastName = "Tester",
    NationalityId = nationalityId,
    DateOfBirth = new DateOnly(2000, 1, 1),
    Address = new Address(nationalityId, "Prague", "10000", "Main", "1"),
    ReasonOfStay = "Tourism",
  };

  [Fact]
  public async Task Handle_EmptyDb_ReturnsZeros()
  {
    Result<GuestStatsByCountryResponse> result =
      await CreateSut().Handle(new GetGuestStatsByCountryQuery(From, To), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.From.ShouldBe(From);
    result.Value.To.ShouldBe(To);
    result.Value.TotalGuests.ShouldBe(0);
    result.Value.TotalPersonNights.ShouldBe(0);
    result.Value.Rows.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_GuestFullyInsideRange_ContributesFullStay()
  {
    var nationalityId = Guid.NewGuid();
    var billId = Guid.NewGuid();
    Db.Nationalities.Add(MakeNationality(nationalityId, "CZ", "CZE", "Cesko", "Czechia"));
    Db.Bills.Add(MakeBill(billId, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 8)));
    Db.Guests.Add(MakeGuest(nationalityId, billId));
    await Db.SaveChangesAsync();

    Result<GuestStatsByCountryResponse> result =
      await CreateSut().Handle(new GetGuestStatsByCountryQuery(From, To), CancellationToken.None);

    result.Value.TotalGuests.ShouldBe(1);
    result.Value.TotalPersonNights.ShouldBe(7);
    result.Value.Rows.Count.ShouldBe(1);
    result.Value.Rows[0].Alpha3.ShouldBe("CZE");
    result.Value.Rows[0].GuestCount.ShouldBe(1);
    result.Value.Rows[0].PersonNights.ShouldBe(7);
  }

  [Fact]
  public async Task Handle_StayExtendsBeforeFrom_ClampsToInRangeNights()
  {
    var nationalityId = Guid.NewGuid();
    var billId = Guid.NewGuid();
    Db.Nationalities.Add(MakeNationality(nationalityId, "CZ", "CZE", "Cesko", "Czechia"));
    Db.Bills.Add(MakeBill(billId, new DateOnly(2026, 5, 28), new DateOnly(2026, 6, 4)));
    Db.Guests.Add(MakeGuest(nationalityId, billId));
    await Db.SaveChangesAsync();

    Result<GuestStatsByCountryResponse> result =
      await CreateSut().Handle(new GetGuestStatsByCountryQuery(From, To), CancellationToken.None);

    result.Value.TotalPersonNights.ShouldBe(3);
    result.Value.Rows[0].PersonNights.ShouldBe(3);
  }

  [Fact]
  public async Task Handle_StayExtendsAfterTo_ClampsToInRangeNights()
  {
    var nationalityId = Guid.NewGuid();
    var billId = Guid.NewGuid();
    Db.Nationalities.Add(MakeNationality(nationalityId, "DE", "DEU", "Nemecko", "Germany"));
    Db.Bills.Add(MakeBill(billId, new DateOnly(2026, 8, 29), new DateOnly(2026, 9, 5)));
    Db.Guests.Add(MakeGuest(nationalityId, billId));
    await Db.SaveChangesAsync();

    Result<GuestStatsByCountryResponse> result =
      await CreateSut().Handle(new GetGuestStatsByCountryQuery(From, To), CancellationToken.None);

    result.Value.TotalPersonNights.ShouldBe(3);
  }

  [Fact]
  public async Task Handle_InlineGuestWithoutBill_IsExcluded()
  {
    var nationalityId = Guid.NewGuid();
    Db.Nationalities.Add(MakeNationality(nationalityId, "SK", "SVK", "Slovensko", "Slovakia"));
    Guest guest = MakeGuest(nationalityId, billId: Guid.Empty);
    guest.BillId = null;
    Db.Guests.Add(guest);
    await Db.SaveChangesAsync();

    Result<GuestStatsByCountryResponse> result =
      await CreateSut().Handle(new GetGuestStatsByCountryQuery(From, To), CancellationToken.None);

    result.Value.TotalGuests.ShouldBe(0);
    result.Value.Rows.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_MultipleGuestsOnOneBill_EachContributesNights()
  {
    var nationalityId = Guid.NewGuid();
    var billId = Guid.NewGuid();
    Db.Nationalities.Add(MakeNationality(nationalityId, "PL", "POL", "Polsko", "Poland"));
    Db.Bills.Add(MakeBill(billId, new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 13)));
    Db.Guests.Add(MakeGuest(nationalityId, billId));
    Db.Guests.Add(MakeGuest(nationalityId, billId));
    Db.Guests.Add(MakeGuest(nationalityId, billId));
    await Db.SaveChangesAsync();

    Result<GuestStatsByCountryResponse> result =
      await CreateSut().Handle(new GetGuestStatsByCountryQuery(From, To), CancellationToken.None);

    result.Value.TotalGuests.ShouldBe(3);
    result.Value.TotalPersonNights.ShouldBe(9);
    result.Value.Rows[0].GuestCount.ShouldBe(3);
    result.Value.Rows[0].PersonNights.ShouldBe(9);
  }

  [Fact]
  public async Task Handle_BillFullyBeforeFrom_NotReturned()
  {
    var nationalityId = Guid.NewGuid();
    var billId = Guid.NewGuid();
    Db.Nationalities.Add(MakeNationality(nationalityId, "AT", "AUT", "Rakousko", "Austria"));
    Db.Bills.Add(MakeBill(billId, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 5)));
    Db.Guests.Add(MakeGuest(nationalityId, billId));
    await Db.SaveChangesAsync();

    Result<GuestStatsByCountryResponse> result =
      await CreateSut().Handle(new GetGuestStatsByCountryQuery(From, To), CancellationToken.None);

    result.Value.TotalGuests.ShouldBe(0);
    result.Value.Rows.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_MultipleNationalities_SortedByPersonNightsDesc()
  {
    var czId = Guid.NewGuid();
    var deId = Guid.NewGuid();
    var skId = Guid.NewGuid();
    Db.Nationalities.AddRange(
      MakeNationality(czId, "CZ", "CZE", "Cesko", "Czechia"),
      MakeNationality(deId, "DE", "DEU", "Nemecko", "Germany"),
      MakeNationality(skId, "SK", "SVK", "Slovensko", "Slovakia"));

    var czBill = Guid.NewGuid();
    var deBill = Guid.NewGuid();
    var skBill = Guid.NewGuid();
    Db.Bills.AddRange(
      MakeBill(czBill, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 6)),
      MakeBill(deBill, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 8)),
      MakeBill(skBill, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 4)));

    Db.Guests.Add(MakeGuest(czId, czBill));
    Db.Guests.Add(MakeGuest(czId, czBill));
    Db.Guests.Add(MakeGuest(deId, deBill));
    Db.Guests.Add(MakeGuest(skId, skBill));
    await Db.SaveChangesAsync();

    Result<GuestStatsByCountryResponse> result =
      await CreateSut().Handle(new GetGuestStatsByCountryQuery(From, To), CancellationToken.None);

    result.Value.Rows.Select(r => r.Alpha3).ToArray().ShouldBe(CzeDeuSvk);
    result.Value.Rows[0].PersonNights.ShouldBe(10);
    result.Value.Rows[1].PersonNights.ShouldBe(7);
    result.Value.Rows[2].PersonNights.ShouldBe(3);
  }
}
