using Application.Finance.Bills.GetLinkableInvoices;
using Domain.Common;
using Domain.Finance.InvoiceItems;
using Domain.Finance.Invoices;
using Domain.Finance.Payers;
using SharedKernel;

namespace Application.UnitTests.Finance.Bills;

public sealed class GetLinkableInvoicesQueryHandlerTests : HandlerTestBase
{
  private GetLinkableInvoicesQueryHandler CreateSut() => new(Db);

  private static Address Addr() => new(Guid.NewGuid(), "Prague", "10000", "Main", "1");

  private static Invoice MakeInvoice(
    Guid id,
    Guid reservationId,
    InvoiceStatus status,
    Guid? linkedBillId = null,
    string? number = "N1") =>
    new()
    {
      Id = id,
      ReservationId = reservationId,
      Status = status,
      Number = number,
      IssuedAt = DateOnly.FromDateTime(DateTime.UtcNow),
      PaidAt = status == InvoiceStatus.Paid ? DateOnly.FromDateTime(DateTime.UtcNow) : null,
      LinkedBillId = linkedBillId,
      Email = "seed@example.com",
      PhoneNumber = "+420000000000",
      Payer = new Payer { Name = "A", Surname = "B", Address = Addr() },
    };

  [Fact]
  public async Task Handle_ReturnsOnlyPaidUnlinkedInvoicesForReservation()
  {
    var target = Guid.NewGuid();
    var other = Guid.NewGuid();

    // Should return: paid, unlinked, target reservation
    var matchId = Guid.NewGuid();
    Db.Invoices.Add(MakeInvoice(matchId, target, InvoiceStatus.Paid));
    Db.InvoiceItems.Add(new InvoiceItem
    {
      Id = Guid.NewGuid(),
      InvoiceId = matchId,
      ServiceGuid = Guid.NewGuid(),
      Quantity = 2m,
      UnitPrice = 500m,
      VatRatePercentage = 21m,
    });

    // Should NOT return: paid, already linked, target reservation
    Db.Invoices.Add(MakeInvoice(Guid.NewGuid(), target, InvoiceStatus.Paid, Guid.NewGuid(), "N2"));

    // Should NOT return: draft, unlinked, target reservation
    Db.Invoices.Add(MakeInvoice(Guid.NewGuid(), target, InvoiceStatus.Draft, null, null));

    // Should NOT return: paid, unlinked, other reservation
    Db.Invoices.Add(MakeInvoice(Guid.NewGuid(), other, InvoiceStatus.Paid, null, "N3"));

    await Db.SaveChangesAsync();

    Result<IReadOnlyList<LinkableInvoiceView>> result = await CreateSut()
      .Handle(new GetLinkableInvoicesQuery(target), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Count.ShouldBe(1);
    result.Value[0].Id.ShouldBe(matchId);
    result.Value[0].TotalAmount.ShouldBe(1000m); // UnitPrice is gross: 2 × 500
  }
}
