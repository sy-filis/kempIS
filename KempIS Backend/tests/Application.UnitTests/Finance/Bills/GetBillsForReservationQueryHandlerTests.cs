using Application.Finance.Bills.GetBillsForReservation;
using Application.Finance.Bills.ListBills;
using Domain.Common;
using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using SharedKernel;

namespace Application.UnitTests.Finance.Bills;

public sealed class GetBillsForReservationQueryHandlerTests : HandlerTestBase
{
  private GetBillsForReservationQueryHandler CreateSut() => new(Db);

  private static Address Addr() => new(Guid.NewGuid(), "Prague", "10000", "Main", "1");

  private static Bill MakeBill(Guid id, string number, Guid reservationId) =>
    new()
    {
      Id = id,
      Number = number,
      Kind = BillKind.Regular,
      ReservationId = reservationId,
      LanguageIdGuid = Guid.NewGuid(),
      IssuedAtUtc = DateTime.UtcNow,
      CheckInAt = new DateOnly(2026, 4, 20),
      CheckOutAt = new DateOnly(2026, 4, 22),
      Payer = new Payer { Name = "A", Surname = "B", Address = Addr() },
      LegalEntity = new LegalEntity { Name = "L", Cin = "1", Tin = "1", Address = Addr() },
      Payment = new Payment(PaymentType.Cash, 100m),
    };

  [Fact]
  public async Task Handle_ReturnsOnlyBillsForReservation()
  {
    var target = Guid.NewGuid();
    var other = Guid.NewGuid();

    Db.Bills.Add(MakeBill(Guid.NewGuid(), "B1", target));
    Db.Bills.Add(MakeBill(Guid.NewGuid(), "B2", target));
    Db.Bills.Add(MakeBill(Guid.NewGuid(), "B3", other));
    await Db.SaveChangesAsync();

    Result<IReadOnlyList<BillSummary>> result = await CreateSut()
      .Handle(new GetBillsForReservationQuery(target), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Count.ShouldBe(2);
    result.Value.ShouldAllBe(s => s.ReservationId == target);
  }
}
