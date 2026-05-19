using Application.Retention;
using Domain.Common;
using Domain.Finance.BillItems;
using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel;

namespace Application.UnitTests.Retention;

public sealed class RunBillAnonymizationCommandHandlerTests : HandlerTestBase
{
  private static readonly Guid CountryId = Guid.NewGuid();
  private static readonly DateTime IssuedAt = new(2026, 4, 22, 18, 0, 0, DateTimeKind.Utc);
  private static readonly DateTime DocumentAt = new(2026, 4, 22, 18, 5, 0, DateTimeKind.Utc);

  private RunBillAnonymizationCommandHandler CreateSut() =>
    new(Db, NullLogger<RunBillAnonymizationCommandHandler>.Instance);

  private static Address Addr() => new(CountryId, "Prague", "10000", "Main", "1");

  private static Bill NewBill(DateOnly? scartation, byte[]? document = null) => new()
  {
    Id = Guid.NewGuid(),
    Number = $"BILL-{Guid.NewGuid():N}"[..12],
    Kind = BillKind.Regular,
    LanguageIdGuid = Guid.NewGuid(),
    IssuedAtUtc = IssuedAt,
    CheckInAt = new DateOnly(2026, 4, 20),
    CheckOutAt = new DateOnly(2026, 4, 22),
    Payer = new Payer { Name = "John", Surname = "Doe", Address = Addr() },
    LegalEntity = new LegalEntity { Name = "Acme s.r.o.", Cin = "12345678", Tin = "CZ12345678", Address = Addr() },
    Payment = new Payment(PaymentType.Card, 100m),
    DocumentContent = document,
    DocumentGeneratedAtUtc = document is null ? null : DocumentAt,
    Scartation = scartation,
  };

  [Fact]
  public async Task Handle_NoDueBills_ReturnsZero()
  {
    var today = new DateOnly(2026, 5, 8);
    Db.Bills.Add(NewBill(scartation: null));
    Db.Bills.Add(NewBill(scartation: today.AddDays(1)));
    await Db.SaveChangesAsync();

    Result<int> result = await CreateSut().Handle(new RunBillAnonymizationCommand(today), CancellationToken.None);

    result.Value.ShouldBe(0);
    Bill held = await Db.Bills.FirstAsync(b => b.Scartation == null);
    held.Payer.Name.ShouldBe("John");
  }

  [Fact]
  public async Task Handle_AnonymizesPayerLegalEntityAndDocumentBlob()
  {
    var today = new DateOnly(2026, 5, 8);
    Bill due = NewBill(scartation: today.AddDays(-1), document: [0x25, 0x50, 0x44, 0x46]);
    Db.Bills.Add(due);
    await Db.SaveChangesAsync();

    Result<int> result = await CreateSut().Handle(new RunBillAnonymizationCommand(today), CancellationToken.None);

    result.Value.ShouldBe(1);
    Bill reloaded = (await Db.Bills.FindAsync(due.Id))!;
    reloaded.Payer.Name.ShouldBe("Anonymized");
    reloaded.Payer.Surname.ShouldBe("Anonymized");
    reloaded.Payer.Address.City.ShouldBe("Anonymized");
    reloaded.Payer.Address.Street.ShouldBe("Anonymized");
    reloaded.Payer.Address.ZipCode.ShouldBe("Anonymized");
    reloaded.Payer.Address.HouseNumber.ShouldBe("Anonymized");
    reloaded.LegalEntity!.Name.ShouldBe("Anonymized");
    reloaded.LegalEntity.Cin.ShouldBe("00000000");
    reloaded.LegalEntity.Tin.ShouldBe("00000000");
    reloaded.LegalEntity.Address.City.ShouldBe("Anonymized");
    reloaded.DocumentContent.ShouldBeNull();
    reloaded.DocumentGeneratedAtUtc.ShouldBeNull();
    reloaded.Scartation.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_PreservesNonPii()
  {
    var today = new DateOnly(2026, 5, 8);
    Bill due = NewBill(scartation: today);
    Db.Bills.Add(due);
    Db.BillItems.Add(new BillItem
    {
      Id = Guid.NewGuid(),
      BillId = due.Id,
      ServiceId = Guid.NewGuid(),
      Quantity = 2u,
      UnitPrice = 500m,
      VatRatePercentage = 21m,
      RecapSingleQuantity = 1u,
      RecapDayQuantity = 2u,
    });
    await Db.SaveChangesAsync();

    await CreateSut().Handle(new RunBillAnonymizationCommand(today), CancellationToken.None);

    Bill reloaded = (await Db.Bills.FindAsync(due.Id))!;
    reloaded.Number.ShouldStartWith("BILL-");
    reloaded.IssuedAtUtc.ShouldBe(IssuedAt);
    reloaded.CheckInAt.ShouldBe(due.CheckInAt);
    reloaded.CheckOutAt.ShouldBe(due.CheckOutAt);
    reloaded.Payment.Amount.ShouldBe(100m);
    reloaded.Payment.PaymentType.ShouldBe(PaymentType.Card);
    (await Db.BillItems.CountAsync(i => i.BillId == due.Id)).ShouldBe(1);
  }

  [Fact]
  public async Task Handle_RerunSameDay_IsNoop()
  {
    var today = new DateOnly(2026, 5, 8);
    Db.Bills.Add(NewBill(scartation: today.AddDays(-1)));
    await Db.SaveChangesAsync();

    Result<int> first = await CreateSut().Handle(new RunBillAnonymizationCommand(today), CancellationToken.None);
    Result<int> second = await CreateSut().Handle(new RunBillAnonymizationCommand(today), CancellationToken.None);

    first.Value.ShouldBe(1);
    second.Value.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_PreservesAddressCountryId()
  {
    var today = new DateOnly(2026, 5, 8);
    Bill due = NewBill(scartation: today.AddDays(-1));
    Db.Bills.Add(due);
    await Db.SaveChangesAsync();

    await CreateSut().Handle(new RunBillAnonymizationCommand(today), CancellationToken.None);

    Bill reloaded = (await Db.Bills.FindAsync(due.Id))!;
    reloaded.Payer.Address.CountryId.ShouldBe(CountryId);
    reloaded.LegalEntity!.Address.CountryId.ShouldBe(CountryId);
  }
}
