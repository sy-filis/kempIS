using Application.Abstractions.Finance;
using Application.Configuration;
using Application.Finance.Bills.CreateRepairBill;
using Application.Finance.Bills.Shared;
using Domain.Common;
using Domain.Finance.BillItems;
using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using SharedKernel;

namespace Application.UnitTests.Finance.Bills;

public sealed class CreateRepairBillCommandHandlerTests : HandlerTestBase
{
  private readonly IBillNumberGenerator _numbers = Substitute.For<IBillNumberGenerator>();
  private readonly CapturingDomainEventsDispatcher _dispatcher = new();

  private readonly RetentionSettings _retentionSettings = new()
  {
    GuestYears = 6,
    BillYears = 10,
    InvoiceYears = 10,
    RunAtLocalTime = new TimeOnly(3, 0),
  };

  protected override ApplicationDbContext CreateDbContext(SqliteConnection connection)
  {
    DbContextOptions<ApplicationDbContext> options =
      new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlite(connection)
        .UseSnakeCaseNamingConvention()
        .Options;

    return new ApplicationDbContext(options, _dispatcher, Clock);
  }

  private CreateRepairBillCommandHandler CreateSut() => new(Db, Clock, _numbers, Options.Create(_retentionSettings));

  private static Address Addr() => new(Guid.NewGuid(), "Prague", "10000", "Main", "1");

  private async Task<(Guid originalBillId, Guid serviceId)> SeedOriginalBill(uint quantity = 3u, decimal unitPrice = 500m, decimal vat = 21m)
  {
    var billId = Guid.NewGuid();
    var serviceId = Guid.NewGuid();
    var payer = new Payer { Name = "John", Surname = "Doe", Address = Addr() };
    var legal = new LegalEntity { Name = "Acme", Cin = "123", Tin = "CZ123", Address = Addr() };

    Db.Bills.Add(new Bill
    {
      Id = billId,
      Number = "2026/0001",
      Kind = BillKind.Regular,
      ReservationId = Guid.NewGuid(),
      LanguageIdGuid = Guid.NewGuid(),
      IssuedAtUtc = DateTime.UtcNow,
      CheckInAt = new DateOnly(2026, 4, 20),
      CheckOutAt = new DateOnly(2026, 4, 22),
      Payer = payer,
      LegalEntity = legal,
      Payment = new Payment(PaymentType.Card, quantity * unitPrice * (1 + vat / 100m)),
    });

    Db.BillItems.Add(new BillItem
    {
      Id = Guid.NewGuid(),
      BillId = billId,
      ServiceId = serviceId,
      Quantity = quantity,
      UnitPrice = unitPrice,
      VatRatePercentage = vat,
      RecapSingleQuantity = 1u,
      RecapDayQuantity = quantity,
    });

    await Db.SaveChangesAsync();
    return (billId, serviceId);
  }

  private static CreateRepairBillCommand MakeRepairCommand(
    Guid originalBillId, Guid serviceId, uint quantity,
    decimal unitPrice = 500m, decimal vat = 21m, string reason = "test reason") =>
    new(
      originalBillId,
      PaymentType.Cash,
      reason,
      [new BillItemInput(serviceId, quantity, unitPrice, vat, 1u, quantity)]);

  [Fact]
  public async Task Handle_HappyPath_UnderCap_Succeeds()
  {
    (Guid originalId, Guid serviceId) = await SeedOriginalBill(quantity: 3u);
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0002");

    Result<CreateRepairBillResponse> result = await CreateSut()
      .Handle(MakeRepairCommand(originalId, serviceId, quantity: 2u), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Bill repair = (await Db.Bills.FindAsync(result.Value.BillId))!;
    repair.Kind.ShouldBe(BillKind.Repair);
    repair.OriginalBillId.ShouldBe(originalId);
    repair.Number.ShouldBe("2026/0002");
  }

  [Fact]
  public async Task Handle_HappyPath_AtCap_Succeeds()
  {
    (Guid originalId, Guid serviceId) = await SeedOriginalBill(quantity: 3u);
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0002");

    Result<CreateRepairBillResponse> result = await CreateSut()
      .Handle(MakeRepairCommand(originalId, serviceId, quantity: 3u), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
  }

  [Fact]
  public async Task Handle_Fails_WhenSingleRepairExceedsCap()
  {
    (Guid originalId, Guid serviceId) = await SeedOriginalBill(quantity: 3u);
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0002");

    Result<CreateRepairBillResponse> result = await CreateSut()
      .Handle(MakeRepairCommand(originalId, serviceId, quantity: 4u), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Bill.RepairQuantityExceedsCap");
  }

  [Fact]
  public async Task Handle_TwoSequentialRepairs_SummingToExactlyOriginal_BothSucceed()
  {
    (Guid originalId, Guid serviceId) = await SeedOriginalBill(quantity: 3u);
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0002", "2026/0003");

    Result<CreateRepairBillResponse> first = await CreateSut()
      .Handle(MakeRepairCommand(originalId, serviceId, 2u), CancellationToken.None);
    first.IsSuccess.ShouldBeTrue();

    Result<CreateRepairBillResponse> second = await CreateSut()
      .Handle(MakeRepairCommand(originalId, serviceId, 1u), CancellationToken.None);
    second.IsSuccess.ShouldBeTrue();
  }

  [Fact]
  public async Task Handle_Fails_WhenCumulativeRepairsExceedOriginal()
  {
    (Guid originalId, Guid serviceId) = await SeedOriginalBill(quantity: 3u);
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0002", "2026/0003");

    Result<CreateRepairBillResponse> first = await CreateSut()
      .Handle(MakeRepairCommand(originalId, serviceId, 2u), CancellationToken.None);
    first.IsSuccess.ShouldBeTrue();

    // qty=2 exhausts the cap of 3 (1 remaining).
    Result<CreateRepairBillResponse> second = await CreateSut()
      .Handle(MakeRepairCommand(originalId, serviceId, 2u), CancellationToken.None);

    second.IsFailure.ShouldBeTrue();
    second.Error.Code.ShouldBe("Bill.RepairQuantityExceedsCap");
  }

  [Fact]
  public async Task Handle_Fails_WhenLineNotOnOriginal()
  {
    (Guid originalId, _) = await SeedOriginalBill();
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0002");

    var otherServiceId = Guid.NewGuid(); // not on original
    Result<CreateRepairBillResponse> result = await CreateSut()
      .Handle(MakeRepairCommand(originalId, otherServiceId, 1u), CancellationToken.None);

    result.Error.Code.ShouldBe("Bill.RepairLineNotOnOriginal");
  }

  [Fact]
  public async Task Handle_Fails_WhenUnitPriceDiffersFromOriginal()
  {
    (Guid originalId, Guid serviceId) = await SeedOriginalBill(unitPrice: 500m);
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0002");

    Result<CreateRepairBillResponse> result = await CreateSut()
      .Handle(MakeRepairCommand(originalId, serviceId, 1u, unitPrice: 400m), CancellationToken.None);

    result.Error.Code.ShouldBe("Bill.RepairLineNotOnOriginal");
  }

  [Fact]
  public async Task Handle_Fails_WhenOriginalIsRepair()
  {
    (Guid firstOriginalId, Guid serviceId) = await SeedOriginalBill();

    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0002", "2026/0003");
    Result<CreateRepairBillResponse> firstRepair = await CreateSut()
      .Handle(MakeRepairCommand(firstOriginalId, serviceId, 1u), CancellationToken.None);
    firstRepair.IsSuccess.ShouldBeTrue();

    Result<CreateRepairBillResponse> result = await CreateSut()
      .Handle(MakeRepairCommand(firstRepair.Value.BillId, serviceId, 1u), CancellationToken.None);

    result.Error.Code.ShouldBe("Bill.OriginalMustBeRegular");
  }

  [Fact]
  public async Task Handle_Fails_WhenOriginalMissing()
  {
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0002");

    Result<CreateRepairBillResponse> result = await CreateSut()
      .Handle(MakeRepairCommand(Guid.NewGuid(), Guid.NewGuid(), 1u), CancellationToken.None);

    result.Error.Code.ShouldBe("Bill.NotFound");
  }

  [Fact]
  public async Task Handle_CopiesPayerLegalEntityLanguage_FromOriginal()
  {
    (Guid originalId, Guid serviceId) = await SeedOriginalBill();
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0002");

    Bill original = (await Db.Bills.FindAsync(originalId))!;

    Result<CreateRepairBillResponse> result = await CreateSut()
      .Handle(MakeRepairCommand(originalId, serviceId, 1u), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Bill repair = (await Db.Bills.FindAsync(result.Value.BillId))!;
    repair.Payer.Name.ShouldBe(original.Payer.Name);
    repair.Payer.Surname.ShouldBe(original.Payer.Surname);
    repair.LegalEntity!.Name.ShouldBe(original.LegalEntity!.Name);
    repair.LegalEntity.Cin.ShouldBe(original.LegalEntity.Cin);
    repair.LanguageIdGuid.ShouldBe(original.LanguageIdGuid);
    repair.CheckInAt.ShouldBe(original.CheckInAt);
    repair.CheckOutAt.ShouldBe(original.CheckOutAt);
    repair.ReservationId.ShouldBe(original.ReservationId);
  }

  [Fact]
  public async Task Handle_SetsScartationFromConfig()
  {
    (Guid originalId, Guid serviceId) = await SeedOriginalBill(quantity: 3u);
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/9999");

    Result<CreateRepairBillResponse> result = await CreateSut()
      .Handle(MakeRepairCommand(originalId, serviceId, quantity: 1u), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Bill repair = (await Db.Bills.FindAsync(result.Value.BillId))!;
    repair.Scartation.ShouldBe(DateOnly.FromDateTime(repair.IssuedAtUtc).AddYears(_retentionSettings.BillYears));
  }

  [Fact]
  public async Task Handle_PersistsRepairBill_WithNegativeAmounts()
  {
    (Guid originalId, Guid serviceId) = await SeedOriginalBill(quantity: 3u, unitPrice: 500m);
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0002");

    Result<CreateRepairBillResponse> result = await CreateSut()
      .Handle(MakeRepairCommand(originalId, serviceId, quantity: 2u, unitPrice: 500m), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();

    Bill repair = (await Db.Bills.FindAsync(result.Value.BillId))!;
    repair.Payment.Amount.ShouldBe(-1000m);

    BillItem repairItem = await Db.BillItems.SingleAsync(i => i.BillId == repair.Id);
    repairItem.UnitPrice.ShouldBe(-500m);
    repairItem.Quantity.ShouldBe(2u);
  }

  [Fact]
  public async Task Handle_PersistsReason_AndRaisesEventCarryingIt()
  {
    (Guid originalId, Guid serviceId) = await SeedOriginalBill(quantity: 3u);
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0002");

    var command = new CreateRepairBillCommand(
      originalId,
      PaymentType.Cash,
      "Wrong service quantity charged",
      [new BillItemInput(serviceId, 1u, 500m, 21m, 1u, 1u)]);

    Result<CreateRepairBillResponse> result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Bill repair = (await Db.Bills.FindAsync(result.Value.BillId))!;
    repair.RepairReason.ShouldBe("Wrong service quantity charged");

    BillRepairedDomainEvent ev = _dispatcher.Dispatched.OfType<BillRepairedDomainEvent>().Single();
    ev.OriginalBillId.ShouldBe(originalId);
    ev.RepairBillId.ShouldBe(repair.Id);
    ev.Reason.ShouldBe("Wrong service quantity charged");
  }

  [Fact]
  public async Task Handle_CapUsesRecapSingleTimesRecapDay_NotQuantity()
  {
    // Seed an original line where Quantity (display field) = 2 but the true billable
    // units (recapSingle × recapDay) = 4. The repair should be able to refund all 4.
    var billId = Guid.NewGuid();
    var serviceId = Guid.NewGuid();
    Db.Bills.Add(new Bill
    {
      Id = billId,
      Number = "2026/1001",
      Kind = BillKind.Regular,
      ReservationId = Guid.NewGuid(),
      LanguageIdGuid = Guid.NewGuid(),
      IssuedAtUtc = DateTime.UtcNow,
      CheckInAt = new DateOnly(2026, 4, 20),
      CheckOutAt = new DateOnly(2026, 4, 22),
      Payer = new Payer { Name = "John", Surname = "Doe", Address = Addr() },
      LegalEntity = new LegalEntity { Name = "Acme", Cin = "123", Tin = "CZ123", Address = Addr() },
      Payment = new Payment(PaymentType.Card, 4m * 500m),
    });
    Db.BillItems.Add(new BillItem
    {
      Id = Guid.NewGuid(),
      BillId = billId,
      ServiceId = serviceId,
      Quantity = 2u,
      UnitPrice = 500m,
      VatRatePercentage = 21m,
      RecapSingleQuantity = 2u,
      RecapDayQuantity = 2u,
    });
    await Db.SaveChangesAsync();
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/1002");

    var command = new CreateRepairBillCommand(
      billId,
      PaymentType.Cash,
      "test reason",
      [new BillItemInput(serviceId, Quantity: 2u, UnitPrice: 500m, VatRatePercentage: 21m,
        RecapSingleQuantity: 2u, RecapDayQuantity: 2u)]);

    Result<CreateRepairBillResponse> result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code + ": " + result.Error.Description : "");

    Bill repair = (await Db.Bills.FindAsync(result.Value.BillId))!;
    // Repair total = -(recapSingle × recapDay × unitPrice) = -(2 × 2 × 500) = -2000
    repair.Payment.Amount.ShouldBe(-2000m);
  }
}
