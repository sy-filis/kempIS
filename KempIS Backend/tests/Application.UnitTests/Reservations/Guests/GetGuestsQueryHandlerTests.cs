using Application.Reservations.Guests;
using Domain.Common;
using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Domain.Reservations.Guests;
using SharedKernel;

namespace Application.UnitTests.Reservations.Guests;

public sealed class GetGuestsQueryHandlerTests : HandlerTestBase
{
  private GetGuestsQueryHandler CreateSut() => new(Db);

  private static Address Addr(string city = "Prague", string street = "Main", string zip = "10000", string house = "1") =>
    new(Guid.NewGuid(), city, zip, street, house);

  private static Bill MakeBill(Guid id, DateOnly checkIn, DateOnly checkOut) => new()
  {
    Id = id,
    Number = "B-" + id.ToString("N")[..6],
    Kind = BillKind.Regular,
    ReservationId = Guid.NewGuid(),
    LanguageIdGuid = Guid.NewGuid(),
    IssuedAtUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
    CheckInAt = checkIn,
    CheckOutAt = checkOut,
    Payer = new Payer { Name = "A", Surname = "B", Address = Addr() },
    LegalEntity = new LegalEntity { Name = "L", Cin = "1", Tin = "1", Address = Addr() },
    Payment = new Payment(PaymentType.Cash, 100m),
  };

  private static Guest MakeGuest(Guid? billId, string firstName = "John", string lastName = "Doe",
    Address? address = null) => new()
    {
      Id = Guid.NewGuid(),
      ReservationId = Guid.NewGuid(),
      BillId = billId,
      FirstName = firstName,
      LastName = lastName,
      NationalityId = Guid.NewGuid(),
      DateOfBirth = new DateOnly(1990, 1, 1),
      DocumentType = DocumentType.Passport,
      DocumentNumber = "X1",
      Address = address ?? Addr(),
      ReasonOfStay = "tourism",
      StayDateRange = new DateRange(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5)),
    };

  [Fact]
  public async Task Handle_NoGuests_ReturnsEmpty()
  {
    Result<List<GuestResponse>> result = await CreateSut()
      .Handle(new GetGuestsQuery(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), null), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_GuestWithoutBill_IsExcluded()
  {
    Db.Guests.Add(MakeGuest(billId: null));
    await Db.SaveChangesAsync();

    Result<List<GuestResponse>> result = await CreateSut()
      .Handle(new GetGuestsQuery(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), null), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_BillOverlapsRange_GuestIsIncluded()
  {
    Bill bill = MakeBill(Guid.NewGuid(), new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 15));
    Db.Bills.Add(bill);
    Db.Guests.Add(MakeGuest(bill.Id));
    await Db.SaveChangesAsync();

    Result<List<GuestResponse>> result = await CreateSut()
      .Handle(new GetGuestsQuery(new DateOnly(2026, 5, 12), new DateOnly(2026, 5, 20), null), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Count.ShouldBe(1);
  }

  [Fact]
  public async Task Handle_BillOutsideRange_GuestIsExcluded()
  {
    Bill bill = MakeBill(Guid.NewGuid(), new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 15));
    Db.Bills.Add(bill);
    Db.Guests.Add(MakeGuest(bill.Id));
    await Db.SaveChangesAsync();

    Result<List<GuestResponse>> result = await CreateSut()
      .Handle(new GetGuestsQuery(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), null), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_BillBoundaryDayTouches_GuestIsIncluded()
  {
    Bill bill = MakeBill(Guid.NewGuid(), new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 15));
    Db.Bills.Add(bill);
    Db.Guests.Add(MakeGuest(bill.Id));
    await Db.SaveChangesAsync();

    Result<List<GuestResponse>> result = await CreateSut()
      .Handle(new GetGuestsQuery(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 10), null), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Count.ShouldBe(1);
  }

  [Fact]
  public async Task Handle_SearchMatchesLastName_GuestIsIncluded()
  {
    Bill bill = MakeBill(Guid.NewGuid(), new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 15));
    Db.Bills.Add(bill);
    Db.Guests.Add(MakeGuest(bill.Id, lastName: "Novak"));
    Db.Guests.Add(MakeGuest(bill.Id, lastName: "Svoboda"));
    await Db.SaveChangesAsync();

    Result<List<GuestResponse>> result = await CreateSut()
      .Handle(new GetGuestsQuery(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), "novak"), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Count.ShouldBe(1);
    result.Value[0].LastName.ShouldBe("Novak");
  }

  [Fact]
  public async Task Handle_SearchMatchesAddressCity_GuestIsIncluded()
  {
    Bill bill = MakeBill(Guid.NewGuid(), new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 15));
    Db.Bills.Add(bill);
    Db.Guests.Add(MakeGuest(bill.Id, address: Addr(city: "Brno")));
    Db.Guests.Add(MakeGuest(bill.Id, address: Addr(city: "Prague")));
    await Db.SaveChangesAsync();

    Result<List<GuestResponse>> result = await CreateSut()
      .Handle(new GetGuestsQuery(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), "brno"), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Count.ShouldBe(1);
  }

  [Fact]
  public async Task Handle_SearchIsCaseInsensitive()
  {
    Bill bill = MakeBill(Guid.NewGuid(), new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 15));
    Db.Bills.Add(bill);
    Db.Guests.Add(MakeGuest(bill.Id, address: Addr(city: "Praha")));
    await Db.SaveChangesAsync();

    Result<List<GuestResponse>> result = await CreateSut()
      .Handle(new GetGuestsQuery(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), "PRAHA"), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Count.ShouldBe(1);
  }

  [Fact]
  public async Task Handle_SearchMisses_ReturnsEmpty()
  {
    Bill bill = MakeBill(Guid.NewGuid(), new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 15));
    Db.Bills.Add(bill);
    Db.Guests.Add(MakeGuest(bill.Id, lastName: "Novak"));
    await Db.SaveChangesAsync();

    Result<List<GuestResponse>> result = await CreateSut()
      .Handle(new GetGuestsQuery(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), "xyzzy"), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_SearchAppliedAfterBillFilter_GuestOutsideDatesExcluded()
  {
    Bill insideBill = MakeBill(Guid.NewGuid(), new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 15));
    Bill outsideBill = MakeBill(Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5));
    Db.Bills.AddRange(insideBill, outsideBill);
    Db.Guests.Add(MakeGuest(insideBill.Id, lastName: "Novak"));
    Db.Guests.Add(MakeGuest(outsideBill.Id, lastName: "Novak"));
    await Db.SaveChangesAsync();

    Result<List<GuestResponse>> result = await CreateSut()
      .Handle(new GetGuestsQuery(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), "novak"), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Count.ShouldBe(1);
  }

  [Fact]
  public async Task Handle_SearchWhitespaceOnly_TreatedAsNoSearch()
  {
    Bill bill = MakeBill(Guid.NewGuid(), new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 15));
    Db.Bills.Add(bill);
    Db.Guests.Add(MakeGuest(bill.Id, lastName: "Alpha"));
    Db.Guests.Add(MakeGuest(bill.Id, lastName: "Beta"));
    await Db.SaveChangesAsync();

    Result<List<GuestResponse>> result = await CreateSut()
      .Handle(new GetGuestsQuery(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), "   "), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Count.ShouldBe(2);
  }
}
