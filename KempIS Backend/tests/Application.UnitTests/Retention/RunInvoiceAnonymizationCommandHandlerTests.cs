using Application.Retention;
using Domain.Common;
using Domain.Finance.InvoiceItems;
using Domain.Finance.Invoices;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel;

namespace Application.UnitTests.Retention;

public sealed class RunInvoiceAnonymizationCommandHandlerTests : HandlerTestBase
{
  private static readonly Guid CountryId = Guid.NewGuid();
  private static readonly DateOnly IssuedAt = new(2026, 4, 22);
  private static readonly DateOnly PaidAt = new(2026, 4, 23);

  private RunInvoiceAnonymizationCommandHandler CreateSut() =>
    new(Db, NullLogger<RunInvoiceAnonymizationCommandHandler>.Instance);

  private static Address Addr() => new(CountryId, "Prague", "10000", "Main", "1");

  private static Invoice NewInvoice(DateOnly? scartation) => new()
  {
    Id = Guid.NewGuid(),
    ReservationId = Guid.NewGuid(),
    Status = InvoiceStatus.Paid,
    Number = $"INV-{Guid.NewGuid():N}"[..10],
    IssuedAt = IssuedAt,
    PaidAt = PaidAt,
    Email = "jane.roe@example.com",
    PhoneNumber = "+420123456789",
    Payer = new Payer { Name = "Jane", Surname = "Roe", Address = Addr() },
    LegalEntity = new LegalEntity { Name = "Beta s.r.o.", Cin = "87654321", Tin = "CZ87654321", Address = Addr() },
    Scartation = scartation,
  };

  [Fact]
  public async Task Handle_NoDueInvoices_ReturnsZero()
  {
    var today = new DateOnly(2026, 5, 8);
    Db.Invoices.Add(NewInvoice(scartation: null));
    Db.Invoices.Add(NewInvoice(scartation: today.AddDays(1)));
    await Db.SaveChangesAsync();

    Result<int> result = await CreateSut().Handle(new RunInvoiceAnonymizationCommand(today), CancellationToken.None);

    result.Value.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_AnonymizesPayerLegalEntityEmailAndPhone()
  {
    var today = new DateOnly(2026, 5, 8);
    Invoice due = NewInvoice(scartation: today.AddDays(-1));
    Db.Invoices.Add(due);
    await Db.SaveChangesAsync();

    Result<int> result = await CreateSut().Handle(new RunInvoiceAnonymizationCommand(today), CancellationToken.None);

    result.Value.ShouldBe(1);
    Invoice reloaded = (await Db.Invoices.FindAsync(due.Id))!;
    reloaded.Payer!.Name.ShouldBe("Anonymized");
    reloaded.Payer.Surname.ShouldBe("Anonymized");
    reloaded.LegalEntity!.Cin.ShouldBe("00000000");
    reloaded.LegalEntity.Tin.ShouldBe("00000000");
    reloaded.Email.ShouldBe("anonymized@anonymized.invalid");
    reloaded.PhoneNumber.ShouldBe("00000000");
    reloaded.Scartation.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_NullPayer_LeavesPayerNullAndStillAnonymizesContactFields()
  {
    var today = new DateOnly(2026, 5, 8);
    Invoice due = NewInvoice(scartation: today.AddDays(-1));
    due.Payer = null;
    Db.Invoices.Add(due);
    await Db.SaveChangesAsync();

    await CreateSut().Handle(new RunInvoiceAnonymizationCommand(today), CancellationToken.None);

    Invoice reloaded = (await Db.Invoices.FindAsync(due.Id))!;
    reloaded.Payer.ShouldBeNull();
    reloaded.LegalEntity!.Name.ShouldBe("Anonymized");
    reloaded.Email.ShouldBe("anonymized@anonymized.invalid");
  }

  [Fact]
  public async Task Handle_NullLegalEntity_LeavesLegalEntityNullAndStillAnonymizesPayer()
  {
    var today = new DateOnly(2026, 5, 8);
    Invoice due = NewInvoice(scartation: today.AddDays(-1));
    due.LegalEntity = null;
    Db.Invoices.Add(due);
    await Db.SaveChangesAsync();

    await CreateSut().Handle(new RunInvoiceAnonymizationCommand(today), CancellationToken.None);

    Invoice reloaded = (await Db.Invoices.FindAsync(due.Id))!;
    reloaded.LegalEntity.ShouldBeNull();
    reloaded.Payer!.Name.ShouldBe("Anonymized");
    reloaded.PhoneNumber.ShouldBe("00000000");
  }

  [Fact]
  public async Task Handle_PreservesNonPii()
  {
    var today = new DateOnly(2026, 5, 8);
    Invoice due = NewInvoice(scartation: today);
    Db.Invoices.Add(due);
    Db.InvoiceItems.Add(new InvoiceItem
    {
      Id = Guid.NewGuid(),
      InvoiceId = due.Id,
      ServiceGuid = Guid.NewGuid(),
      Quantity = 2m,
      UnitPrice = 500m,
      VatRatePercentage = 21m,
    });
    await Db.SaveChangesAsync();

    await CreateSut().Handle(new RunInvoiceAnonymizationCommand(today), CancellationToken.None);

    Invoice reloaded = (await Db.Invoices.FindAsync(due.Id))!;
    reloaded.Number.ShouldStartWith("INV-");
    reloaded.IssuedAt.ShouldBe(IssuedAt);
    reloaded.PaidAt.ShouldBe(PaidAt);
    reloaded.Status.ShouldBe(InvoiceStatus.Paid);
    (await Db.InvoiceItems.CountAsync(i => i.InvoiceId == due.Id)).ShouldBe(1);
  }

  [Fact]
  public async Task Handle_RerunSameDay_IsNoop()
  {
    var today = new DateOnly(2026, 5, 8);
    Db.Invoices.Add(NewInvoice(scartation: today.AddDays(-1)));
    await Db.SaveChangesAsync();

    await CreateSut().Handle(new RunInvoiceAnonymizationCommand(today), CancellationToken.None);
    Result<int> second = await CreateSut().Handle(new RunInvoiceAnonymizationCommand(today), CancellationToken.None);

    second.Value.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_PreservesAddressCountryId()
  {
    var today = new DateOnly(2026, 5, 8);
    Invoice due = NewInvoice(scartation: today.AddDays(-1));
    Db.Invoices.Add(due);
    await Db.SaveChangesAsync();

    await CreateSut().Handle(new RunInvoiceAnonymizationCommand(today), CancellationToken.None);

    Invoice reloaded = (await Db.Invoices.FindAsync(due.Id))!;
    reloaded.Payer!.Address.CountryId.ShouldBe(CountryId);
    reloaded.LegalEntity!.Address.CountryId.ShouldBe(CountryId);
  }
}
