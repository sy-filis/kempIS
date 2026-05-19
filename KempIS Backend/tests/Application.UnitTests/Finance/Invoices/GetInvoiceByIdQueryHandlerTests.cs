using Application.Finance.Invoices.GetInvoiceById;
using Domain.Common;
using Domain.Finance.InvoiceItems;
using Domain.Finance.Invoices;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using SharedKernel;

namespace Application.UnitTests.Finance.Invoices;

public sealed class GetInvoiceByIdQueryHandlerTests : HandlerTestBase
{
  private GetInvoiceByIdQueryHandler CreateSut() => new(Db);

  private static Address Addr() => new(Guid.NewGuid(), "Prague", "10000", "Main", "1");

  [Fact]
  public async Task Handle_ReturnsInvoiceWithItems_PayerOnly()
  {
    var invoiceId = Guid.NewGuid();
    var reservationId = Guid.NewGuid();
    Db.Invoices.Add(new Invoice
    {
      Id = invoiceId,
      ReservationId = reservationId,
      Status = InvoiceStatus.Paid,
      Number = "EXT-1",
      IssuedAt = new DateOnly(2026, 4, 1),
      PaidAt = new DateOnly(2026, 4, 2),
      Email = "billing@example.com",
      PhoneNumber = "+420123456789",
      Payer = new Payer { Name = "A", Surname = "B", Address = Addr() },
    });
    Db.InvoiceItems.Add(new InvoiceItem
    {
      Id = Guid.NewGuid(),
      InvoiceId = invoiceId,
      ServiceGuid = Guid.NewGuid(),
      Quantity = 2m,
      UnitPrice = 500m,
      VatRatePercentage = 21m,
    });
    Db.InvoiceItems.Add(new InvoiceItem
    {
      Id = Guid.NewGuid(),
      InvoiceId = invoiceId,
      ServiceGuid = Guid.NewGuid(),
      Quantity = 1m,
      UnitPrice = 100m,
      VatRatePercentage = 21m,
    });
    await Db.SaveChangesAsync();

    Result<GetInvoiceByIdResponse> result = await CreateSut()
      .Handle(new GetInvoiceByIdQuery(invoiceId), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Id.ShouldBe(invoiceId);
    result.Value.ReservationId.ShouldBe(reservationId);
    result.Value.Number.ShouldBe("EXT-1");
    result.Value.Status.ShouldBe(InvoiceStatus.Paid);
    result.Value.Email.ShouldBe("billing@example.com");
    result.Value.PhoneNumber.ShouldBe("+420123456789");
    result.Value.Payer.ShouldNotBeNull();
    result.Value.Payer!.Name.ShouldBe("A");
    result.Value.LegalEntity.ShouldBeNull();
    result.Value.Items.Count.ShouldBe(2);
  }

  [Fact]
  public async Task Handle_ReturnsInvoice_LegalEntityOnly()
  {
    var invoiceId = Guid.NewGuid();
    Db.Invoices.Add(new Invoice
    {
      Id = invoiceId,
      ReservationId = Guid.NewGuid(),
      Status = InvoiceStatus.Draft,
      IssuedAt = new DateOnly(2026, 4, 1),
      Email = "billing@example.com",
      PhoneNumber = "+420123456789",
      LegalEntity = new LegalEntity { Name = "Acme", Cin = "1", Tin = "CZ1", Address = Addr() },
    });
    await Db.SaveChangesAsync();

    Result<GetInvoiceByIdResponse> result = await CreateSut()
      .Handle(new GetInvoiceByIdQuery(invoiceId), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Payer.ShouldBeNull();
    result.Value.LegalEntity.ShouldNotBeNull();
    result.Value.LegalEntity!.Name.ShouldBe("Acme");
  }

  [Fact]
  public async Task Handle_ReturnsDueTo_WhenSet()
  {
    var invoiceId = Guid.NewGuid();
    var due = new DateOnly(2026, 6, 1);
    Db.Invoices.Add(new Invoice
    {
      Id = invoiceId,
      ReservationId = Guid.NewGuid(),
      Status = InvoiceStatus.Created,
      Number = "EXT-2",
      IssuedAt = new DateOnly(2026, 4, 1),
      DueTo = due,
      Email = "billing@example.com",
      PhoneNumber = "+420123456789",
      Payer = new Payer { Name = "A", Surname = "B", Address = Addr() },
    });
    await Db.SaveChangesAsync();

    Result<GetInvoiceByIdResponse> result = await CreateSut()
      .Handle(new GetInvoiceByIdQuery(invoiceId), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.DueTo.ShouldBe(due);
  }

  [Fact]
  public async Task Handle_ReturnsNotFound_WhenMissing()
  {
    Result<GetInvoiceByIdResponse> result = await CreateSut()
      .Handle(new GetInvoiceByIdQuery(Guid.NewGuid()), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Invoice.NotFound");
  }
}
