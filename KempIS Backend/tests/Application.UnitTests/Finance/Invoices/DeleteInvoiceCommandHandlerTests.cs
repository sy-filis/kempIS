using Application.Finance.Invoices.DeleteInvoice;
using Domain.Common;
using Domain.Finance.InvoiceItems;
using Domain.Finance.Invoices;
using Domain.Finance.Payers;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Finance.Invoices;

public sealed class DeleteInvoiceCommandHandlerTests : HandlerTestBase
{
  private DeleteInvoiceCommandHandler CreateSut() => new(Db);

  private static Address Addr() => new(Guid.NewGuid(), "Prague", "10000", "Main", "1");

  [Fact]
  public async Task Handle_RemovesInvoiceAndItems_WhenDraft()
  {
    var id = Guid.NewGuid();
    Db.Invoices.Add(new Invoice
    {
      Id = id,
      ReservationId = Guid.NewGuid(),
      Status = InvoiceStatus.Draft,
      IssuedAt = DateOnly.FromDateTime(DateTime.UtcNow),
      Email = "seed@example.com",
      PhoneNumber = "+420000000000",
      Payer = new Payer { Name = "A", Surname = "B", Address = Addr() },
    });
    Db.InvoiceItems.Add(new InvoiceItem
    {
      Id = Guid.NewGuid(),
      InvoiceId = id,
      ServiceGuid = Guid.NewGuid(),
      Quantity = 1m,
      UnitPrice = 1m,
      VatRatePercentage = 21m,
    });
    await Db.SaveChangesAsync();

    Result result = await CreateSut().Handle(new DeleteInvoiceCommand(id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    (await Db.Invoices.FindAsync(id)).ShouldBeNull();
    (await Db.InvoiceItems.CountAsync(i => i.InvoiceId == id)).ShouldBe(0);
  }

  [Fact]
  public async Task Handle_ReturnsNotFound_WhenInvoiceMissing()
  {
    Result result = await CreateSut()
      .Handle(new DeleteInvoiceCommand(Guid.NewGuid()), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Invoice.NotFound");
  }

  [Fact]
  public async Task Handle_ReturnsNotDraft_WhenCreated()
  {
    var id = Guid.NewGuid();
    Db.Invoices.Add(new Invoice
    {
      Id = id,
      ReservationId = Guid.NewGuid(),
      Status = InvoiceStatus.Created,
      Number = "EXT",
      IssuedAt = DateOnly.FromDateTime(DateTime.UtcNow),
      Email = "seed@example.com",
      PhoneNumber = "+420000000000",
      Payer = new Payer { Name = "A", Surname = "B", Address = Addr() },
    });
    await Db.SaveChangesAsync();

    Result result = await CreateSut().Handle(new DeleteInvoiceCommand(id), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Invoice.NotDraft");
  }
}
