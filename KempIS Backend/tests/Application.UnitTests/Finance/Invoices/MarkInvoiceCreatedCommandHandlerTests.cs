using Application.Finance.Invoices.MarkInvoiceCreated;
using Domain.Common;
using Domain.Finance.Invoices;
using Domain.Finance.Payers;
using SharedKernel;

namespace Application.UnitTests.Finance.Invoices;

public sealed class MarkInvoiceCreatedCommandHandlerTests : HandlerTestBase
{
  private MarkInvoiceCreatedCommandHandler CreateSut() => new(Db);

  private static Address Addr() => new(Guid.NewGuid(), "Prague", "10000", "Main", "1");

  private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

  private async Task<Guid> SeedDraft()
  {
    var id = Guid.NewGuid();
    Db.Invoices.Add(new Invoice
    {
      Id = id,
      ReservationId = Guid.NewGuid(),
      Status = InvoiceStatus.Draft,
      IssuedAt = Today(),
      Email = "seed@example.com",
      PhoneNumber = "+420000000000",
      Payer = new Payer { Name = "A", Surname = "B", Address = Addr() },
    });
    await Db.SaveChangesAsync();
    return id;
  }

  [Fact]
  public async Task Handle_TransitionsToCreated_WithNumberIssuedAtAndDueTo()
  {
    Guid id = await SeedDraft();
    var issuedAt = new DateOnly(2026, 4, 22);
    var due = new DateOnly(2026, 5, 6);

    Result result = await CreateSut()
      .Handle(new MarkInvoiceCreatedCommand(id, "EXT-2026-001", issuedAt, due), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();

    Invoice inv = (await Db.Invoices.FindAsync(id))!;
    inv.Status.ShouldBe(InvoiceStatus.Created);
    inv.Number.ShouldBe("EXT-2026-001");
    inv.IssuedAt.ShouldBe(issuedAt);
    inv.DueTo.ShouldBe(due);
  }

  [Fact]
  public async Task Handle_ReturnsNotFound_WhenMissing()
  {
    DateOnly today = Today();
    Result result = await CreateSut()
      .Handle(new MarkInvoiceCreatedCommand(Guid.NewGuid(), "X", today, today.AddDays(14)), CancellationToken.None);
    result.Error.Code.ShouldBe("Invoice.NotFound");
  }

  [Fact]
  public async Task Handle_ReturnsNotDraft_WhenAlreadyCreated()
  {
    Guid id = await SeedDraft();
    DateOnly today = Today();
    await CreateSut().Handle(new MarkInvoiceCreatedCommand(id, "EXT-A", today, today.AddDays(14)), CancellationToken.None);

    Result result = await CreateSut()
      .Handle(new MarkInvoiceCreatedCommand(id, "EXT-B", today, today.AddDays(14)), CancellationToken.None);
    result.Error.Code.ShouldBe("Invoice.NotDraft");
  }

  [Fact]
  public async Task Handle_ReturnsNumberAlreadyUsed_WhenDuplicate()
  {
    Guid first = await SeedDraft();
    Guid second = await SeedDraft();
    DateOnly today = Today();

    await CreateSut().Handle(new MarkInvoiceCreatedCommand(first, "DUP", today, today.AddDays(14)), CancellationToken.None);

    Result result = await CreateSut()
      .Handle(new MarkInvoiceCreatedCommand(second, "DUP", today, today.AddDays(14)), CancellationToken.None);

    result.Error.Code.ShouldBe("Invoice.NumberAlreadyUsed");
  }
}
