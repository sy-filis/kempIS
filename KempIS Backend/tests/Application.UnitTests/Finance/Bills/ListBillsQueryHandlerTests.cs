using Application.Finance.Bills.ListBills;
using Domain.Common;
using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using SharedKernel;

namespace Application.UnitTests.Finance.Bills;

public sealed class ListBillsQueryHandlerTests : HandlerTestBase
{
  private ListBillsQueryHandler CreateSut() => new(Db);

  private static Address Addr() => new(Guid.NewGuid(), "Prague", "10000", "Main", "1");

  private static Bill MakeBill(
    Guid id,
    string number,
    BillKind kind,
    Guid? reservationId,
    DateOnly checkIn,
    DateOnly checkOut,
    DateTime issuedAt,
    decimal amount,
    Guid? financialClosingId = null) =>
    new()
    {
      Id = id,
      Number = number,
      Kind = kind,
      ReservationId = reservationId,
      LanguageIdGuid = Guid.NewGuid(),
      IssuedAtUtc = issuedAt,
      CheckInAt = checkIn,
      CheckOutAt = checkOut,
      FinancialClosingId = financialClosingId,
      Payer = new Payer { Name = "A", Surname = "B", Address = Addr() },
      LegalEntity = new LegalEntity { Name = "L", Cin = "1", Tin = "1", Address = Addr() },
      Payment = new Payment(PaymentType.Cash, amount),
    };

  [Fact]
  public async Task Handle_ReturnsAllBills_WhenNoFilters()
  {
    Db.Bills.Add(MakeBill(Guid.NewGuid(), "B1", BillKind.Regular, Guid.NewGuid(),
      new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 5),
      new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc), 500m));
    Db.Bills.Add(MakeBill(Guid.NewGuid(), "B2", BillKind.Repair, Guid.NewGuid(),
      new DateOnly(2026, 4, 6), new DateOnly(2026, 4, 10),
      new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc), 200m));
    Db.Bills.Add(MakeBill(Guid.NewGuid(), "B3", BillKind.Regular, Guid.NewGuid(),
      new DateOnly(2026, 4, 11), new DateOnly(2026, 4, 15),
      new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc), 300m));
    await Db.SaveChangesAsync();

    Result<IReadOnlyList<BillSummary>> result = await CreateSut()
      .Handle(new ListBillsQuery(null, null, null, null, null, null), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Count.ShouldBe(3);
    result.Value[0].IssuedAtUtc.ShouldBe(new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc));
  }

  [Fact]
  public async Task Handle_FiltersByKind()
  {
    Db.Bills.Add(MakeBill(Guid.NewGuid(), "R1", BillKind.Regular, Guid.NewGuid(),
      new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 5),
      new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc), 500m));
    Db.Bills.Add(MakeBill(Guid.NewGuid(), "REP1", BillKind.Repair, Guid.NewGuid(),
      new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 5),
      new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc), 100m));
    await Db.SaveChangesAsync();

    Result<IReadOnlyList<BillSummary>> result = await CreateSut()
      .Handle(new ListBillsQuery(null, null, null, BillKind.Repair, null, null), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Count.ShouldBe(1);
    result.Value[0].Kind.ShouldBe(BillKind.Repair);
  }

  [Fact]
  public async Task Handle_FiltersByReservationId()
  {
    var targetReservation = Guid.NewGuid();
    Db.Bills.Add(MakeBill(Guid.NewGuid(), "B1", BillKind.Regular, targetReservation,
      new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 5),
      new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc), 500m));
    Db.Bills.Add(MakeBill(Guid.NewGuid(), "B2", BillKind.Regular, Guid.NewGuid(),
      new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 5),
      new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc), 300m));
    await Db.SaveChangesAsync();

    Result<IReadOnlyList<BillSummary>> result = await CreateSut()
      .Handle(new ListBillsQuery(null, null, targetReservation, null, null, null), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Count.ShouldBe(1);
    result.Value[0].ReservationId.ShouldBe(targetReservation);
  }

  [Fact]
  public async Task Handle_FiltersByFinancialClosingId()
  {
    var closingId = Guid.NewGuid();
    Db.Bills.Add(MakeBill(Guid.NewGuid(), "B1", BillKind.Regular, Guid.NewGuid(),
      new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 5),
      new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc), 500m, closingId));
    Db.Bills.Add(MakeBill(Guid.NewGuid(), "B2", BillKind.Regular, Guid.NewGuid(),
      new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 5),
      new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc), 300m));
    await Db.SaveChangesAsync();

    Result<IReadOnlyList<BillSummary>> result = await CreateSut()
      .Handle(new ListBillsQuery(null, null, null, null, closingId, null), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Count.ShouldBe(1);
  }

  [Fact]
  public async Task Handle_FiltersByClosedTrue_ReturnsOnlyClosedBills()
  {
    var closingId = Guid.NewGuid();
    Db.Bills.Add(MakeBill(Guid.NewGuid(), "CLOSED", BillKind.Regular, Guid.NewGuid(),
      new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 5),
      new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc), 500m, closingId));
    Db.Bills.Add(MakeBill(Guid.NewGuid(), "OPEN", BillKind.Regular, Guid.NewGuid(),
      new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 5),
      new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc), 300m));
    await Db.SaveChangesAsync();

    Result<IReadOnlyList<BillSummary>> result = await CreateSut()
      .Handle(new ListBillsQuery(null, null, null, null, null, true), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Count.ShouldBe(1);
    result.Value[0].Number.ShouldBe("CLOSED");
    result.Value[0].FinancialClosingId.ShouldBe(closingId);
  }

  [Fact]
  public async Task Handle_FiltersByClosedFalse_ReturnsOnlyOpenBills()
  {
    var closingId = Guid.NewGuid();
    Db.Bills.Add(MakeBill(Guid.NewGuid(), "CLOSED", BillKind.Regular, Guid.NewGuid(),
      new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 5),
      new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc), 500m, closingId));
    Db.Bills.Add(MakeBill(Guid.NewGuid(), "OPEN", BillKind.Regular, Guid.NewGuid(),
      new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 5),
      new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc), 300m));
    await Db.SaveChangesAsync();

    Result<IReadOnlyList<BillSummary>> result = await CreateSut()
      .Handle(new ListBillsQuery(null, null, null, null, null, false), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Count.ShouldBe(1);
    result.Value[0].Number.ShouldBe("OPEN");
    result.Value[0].FinancialClosingId.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_FiltersByStayOverlap()
  {
    // Bill stays: Apr 1-5, Apr 8-12, Apr 20-25
    Db.Bills.Add(MakeBill(Guid.NewGuid(), "B1", BillKind.Regular, Guid.NewGuid(),
      new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 5),
      new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc), 100m));
    Db.Bills.Add(MakeBill(Guid.NewGuid(), "B2", BillKind.Regular, Guid.NewGuid(),
      new DateOnly(2026, 4, 8), new DateOnly(2026, 4, 12),
      new DateTime(2026, 4, 12, 0, 0, 0, DateTimeKind.Utc), 200m));
    Db.Bills.Add(MakeBill(Guid.NewGuid(), "B3", BillKind.Regular, Guid.NewGuid(),
      new DateOnly(2026, 4, 20), new DateOnly(2026, 4, 25),
      new DateTime(2026, 4, 25, 0, 0, 0, DateTimeKind.Utc), 300m));
    await Db.SaveChangesAsync();

    // Filter: From=Apr 7, To=Apr 15 - overlaps B2 (checkIn Apr 8, checkOut Apr 12)
    var from = new DateOnly(2026, 4, 7);
    var to = new DateOnly(2026, 4, 15);

    Result<IReadOnlyList<BillSummary>> result = await CreateSut()
      .Handle(new ListBillsQuery(from, to, null, null, null, null), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Count.ShouldBe(1);
    result.Value[0].Number.ShouldBe("B2");
  }
}
