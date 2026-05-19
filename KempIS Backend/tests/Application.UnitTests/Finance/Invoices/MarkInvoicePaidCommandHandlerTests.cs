using Application.Finance.Invoices.MarkInvoicePaid;
using Domain.Common;
using Domain.Finance.Invoices;
using Domain.Finance.Payers;
using SharedKernel;

namespace Application.UnitTests.Finance.Invoices;

public sealed class MarkInvoicePaidCommandHandlerTests : HandlerTestBase
{
  private MarkInvoicePaidCommandHandler CreateSut() => new(Db);

  private static Address Addr() => new(Guid.NewGuid(), "Prague", "10000", "Main", "1");

  private async Task<Guid> SeedCreated()
  {
    var id = Guid.NewGuid();
    Db.Invoices.Add(new Invoice
    {
      Id = id,
      ReservationId = Guid.NewGuid(),
      Status = InvoiceStatus.Created,
      Number = "EXT-2026-001",
      IssuedAt = DateOnly.FromDateTime(DateTime.UtcNow),
      Email = "seed@example.com",
      PhoneNumber = "+420000000000",
      Payer = new Payer { Name = "A", Surname = "B", Address = Addr() },
    });
    await Db.SaveChangesAsync();
    return id;
  }

  private async Task<Guid> SeedPaid()
  {
    var id = Guid.NewGuid();
    Db.Invoices.Add(new Invoice
    {
      Id = id,
      ReservationId = Guid.NewGuid(),
      Status = InvoiceStatus.Paid,
      Number = "EXT-2026-002",
      IssuedAt = DateOnly.FromDateTime(DateTime.UtcNow),
      PaidAt = DateOnly.FromDateTime(DateTime.UtcNow),
      Email = "seed@example.com",
      PhoneNumber = "+420000000000",
      Payer = new Payer { Name = "A", Surname = "B", Address = Addr() },
    });
    await Db.SaveChangesAsync();
    return id;
  }

  private async Task<Guid> SeedDraft()
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
    await Db.SaveChangesAsync();
    return id;
  }

  [Fact]
  public async Task Handle_TransitionsToPaid_WithPaidAt()
  {
    Guid id = await SeedCreated();
    var paidAt = new DateOnly(2026, 4, 22);

    Result result = await CreateSut()
      .Handle(new MarkInvoicePaidCommand(id, paidAt), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();

    Invoice inv = (await Db.Invoices.FindAsync(id))!;
    inv.Status.ShouldBe(InvoiceStatus.Paid);
    inv.PaidAt.ShouldBe(paidAt);
    inv.Number.ShouldBe("EXT-2026-001");
  }

  [Fact]
  public async Task Handle_ReturnsNotFound_WhenMissing()
  {
    Result result = await CreateSut()
      .Handle(new MarkInvoicePaidCommand(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow)), CancellationToken.None);
    result.Error.Code.ShouldBe("Invoice.NotFound");
  }

  [Fact]
  public async Task Handle_ReturnsNotCreated_WhenDraft()
  {
    Guid id = await SeedDraft();

    Result result = await CreateSut()
      .Handle(new MarkInvoicePaidCommand(id, DateOnly.FromDateTime(DateTime.UtcNow)), CancellationToken.None);

    result.Error.Code.ShouldBe("Invoice.NotCreated");
  }

  [Fact]
  public async Task Handle_ReturnsNotCreated_WhenAlreadyPaid()
  {
    Guid id = await SeedPaid();

    Result result = await CreateSut()
      .Handle(new MarkInvoicePaidCommand(id, DateOnly.FromDateTime(DateTime.UtcNow)), CancellationToken.None);

    result.Error.Code.ShouldBe("Invoice.NotCreated");
  }
}
