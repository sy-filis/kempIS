using Application.Finance.Invoices.Shared;
using Application.Finance.Invoices.UpdateInvoice;
using Domain.Common;
using Domain.Finance.InvoiceItems;
using Domain.Finance.Invoices;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Finance.Invoices;

public sealed class UpdateInvoiceCommandHandlerTests : HandlerTestBase
{
  private UpdateInvoiceCommandHandler CreateSut() => new(Db);

  private static Address Addr() => new(Guid.NewGuid(), "Prague", "10000", "Main", "1");

  private async Task<Guid> SeedDraftInvoice()
  {
    var id = Guid.NewGuid();
    Db.Invoices.Add(new Invoice
    {
      Id = id,
      ReservationId = Guid.NewGuid(),
      Status = InvoiceStatus.Draft,
      IssuedAt = DateOnly.FromDateTime(DateTime.UtcNow),
      Email = "old@example.com",
      PhoneNumber = "+420000000000",
      Payer = new Payer { Name = "A", Surname = "B", Address = Addr() },
    });
    Db.InvoiceItems.Add(new InvoiceItem
    {
      Id = Guid.NewGuid(),
      InvoiceId = id,
      ServiceGuid = Guid.NewGuid(),
      Quantity = 1m,
      UnitPrice = 100m,
      VatRatePercentage = 21m,
    });
    await Db.SaveChangesAsync();
    return id;
  }

  private static readonly Guid NewServiceId = Guid.NewGuid();

  private static UpdateInvoiceCommand WithLegalEntity(Guid id) => new(
    id,
    Payer: null,
    new InvoiceLegalEntityInput("NewCo", "99", "CZ99", Addr()),
    Email: "new@example.com",
    PhoneNumber: "+420987654321",
    Items: [new InvoiceItemInput(NewServiceId, 3m, 700m, 15m)]);

  private static UpdateInvoiceCommand WithPayer(Guid id) => new(
    id,
    new InvoicePayerInput("New", "Payer", Addr()),
    LegalEntity: null,
    Email: "new@example.com",
    PhoneNumber: "+420987654321",
    Items: [new InvoiceItemInput(NewServiceId, 3m, 700m, 15m)]);

  [Fact]
  public async Task Handle_SwapsPayerForLegalEntityAndUpdatesItems_WhenDraft()
  {
    Guid id = await SeedDraftInvoice();

    Result result = await CreateSut().Handle(WithLegalEntity(id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();

    Invoice invoice = (await Db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id))!;
    invoice.Payer.ShouldBeNull();
    invoice.LegalEntity.ShouldNotBeNull();
    invoice.LegalEntity!.Name.ShouldBe("NewCo");
    invoice.Email.ShouldBe("new@example.com");
    invoice.PhoneNumber.ShouldBe("+420987654321");

    List<InvoiceItem> items = [.. Db.InvoiceItems.Where(i => i.InvoiceId == id)];
    items.Count.ShouldBe(1);
    items[0].ServiceGuid.ShouldBe(NewServiceId);
    items[0].Quantity.ShouldBe(3m);
    items[0].UnitPrice.ShouldBe(700m);
  }

  [Fact]
  public async Task Handle_ReplacesPayerWhilePreservingShape_WhenDraft()
  {
    Guid id = await SeedDraftInvoice();

    Result result = await CreateSut().Handle(WithPayer(id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();

    Invoice invoice = (await Db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id))!;
    invoice.Payer.ShouldNotBeNull();
    invoice.Payer!.Name.ShouldBe("New");
    invoice.LegalEntity.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_ReturnsNotFound_WhenInvoiceMissing()
  {
    Result result = await CreateSut()
      .Handle(WithLegalEntity(Guid.NewGuid()), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Invoice.NotFound");
  }

  [Fact]
  public async Task Handle_UpdatesDueTo_WhenDraft()
  {
    Guid id = await SeedDraftInvoice();
    var due = new DateOnly(2026, 6, 15);

    UpdateInvoiceCommand command = WithPayer(id) with { DueTo = due };
    Result result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();

    Invoice invoice = (await Db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id))!;
    invoice.DueTo.ShouldBe(due);
  }

  [Fact]
  public async Task Handle_ClearsDueTo_WhenOmittedFromUpdate()
  {
    var id = Guid.NewGuid();
    Db.Invoices.Add(new Invoice
    {
      Id = id,
      ReservationId = Guid.NewGuid(),
      Status = InvoiceStatus.Draft,
      IssuedAt = DateOnly.FromDateTime(DateTime.UtcNow),
      DueTo = new DateOnly(2026, 6, 1),
      Email = "old@example.com",
      PhoneNumber = "+420000000000",
      Payer = new Payer { Name = "A", Surname = "B", Address = Addr() },
    });
    await Db.SaveChangesAsync();

    Result result = await CreateSut().Handle(WithPayer(id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();

    Invoice invoice = (await Db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id))!;
    invoice.DueTo.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_ReturnsNotDraft_WhenInvoiceAlreadyCreated()
  {
    var id = Guid.NewGuid();
    Db.Invoices.Add(new Invoice
    {
      Id = id,
      ReservationId = Guid.NewGuid(),
      Status = InvoiceStatus.Created,
      Number = "EXT-1",
      IssuedAt = DateOnly.FromDateTime(DateTime.UtcNow),
      Email = "old@example.com",
      PhoneNumber = "+420000000000",
      LegalEntity = new LegalEntity { Name = "L", Cin = "1", Tin = "1", Address = Addr() },
    });
    await Db.SaveChangesAsync();

    Result result = await CreateSut().Handle(WithPayer(id), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Invoice.NotDraft");
  }
}
