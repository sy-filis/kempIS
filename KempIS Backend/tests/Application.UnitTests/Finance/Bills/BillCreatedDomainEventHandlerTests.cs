using Application.Finance.Bills;
using Domain.Common;
using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute.ExceptionExtensions;
using SharedKernel;

namespace Application.UnitTests.Finance.Bills;

public sealed class BillCreatedDomainEventHandlerTests : HandlerTestBase
{
  private readonly IBillDocumentRenderer _renderer = Substitute.For<IBillDocumentRenderer>();

  private BillCreatedDomainEventHandler CreateSut() =>
    new(Db, _renderer, Clock, NullLogger<BillCreatedDomainEventHandler>.Instance);

  private static Address MinAddress() => new(
    Guid.NewGuid(),
    "Prague",
    "10000",
    "Main St",
    "1");

  private static Bill NewBill(Guid id, string number) =>
    new()
    {
      Id = id,
      Number = number,
      ReservationId = Guid.NewGuid(),
      IssuedAtUtc = DateTime.UtcNow,
      CheckInAt = new DateOnly(2026, 4, 20),
      CheckOutAt = new DateOnly(2026, 4, 22),
      LanguageIdGuid = Guid.NewGuid(),
      Payer = new Payer
      {
        Name = "John",
        Surname = "Doe",
        Address = MinAddress(),
      },
      LegalEntity = new LegalEntity
      {
        Name = "Acme s.r.o.",
        Address = MinAddress(),
        Cin = "12345678",
        Tin = "CZ12345678",
      },
      Payment = new Payment(PaymentType.Cash, 100m),
    };

  [Fact]
  public async Task Handle_PersistsRenderedDocument_OnSuccess()
  {
    var billId = Guid.NewGuid();
    Db.Bills.Add(NewBill(billId, "2024-001"));
    await Db.SaveChangesAsync();

    _renderer.RenderAsync(billId, Arg.Any<CancellationToken>())
      .Returns(Result.Success(new BillDocumentRenderResult([0xAB, 0xCD], "application/pdf", "cs-CZ")));

    await CreateSut().Handle(new BillCreatedDomainEvent(billId), default);

    Bill? stored = await Db.Bills.AsNoTracking().FirstOrDefaultAsync(b => b.Id == billId);
    stored.ShouldNotBeNull();
    stored.DocumentContent.ShouldBe([0xAB, 0xCD]);
    stored.DocumentGeneratedAtUtc.ShouldNotBeNull();
  }

  [Fact]
  public async Task Handle_SwallowsRenderFailure_AndDoesNotPersist()
  {
    var billId = Guid.NewGuid();
    Db.Bills.Add(NewBill(billId, "2024-002"));
    await Db.SaveChangesAsync();

    _renderer.RenderAsync(billId, Arg.Any<CancellationToken>())
      .Returns(Result.Failure<BillDocumentRenderResult>(Error.Problem("Render.Failed", "boom")));

    await Should.NotThrowAsync(() =>
      CreateSut().Handle(new BillCreatedDomainEvent(billId), default));

    Bill? bill = await Db.Bills.AsNoTracking().FirstOrDefaultAsync(b => b.Id == billId);
    bill.ShouldNotBeNull();
    bill.DocumentContent.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_SwallowsRendererException()
  {
    var billId = Guid.NewGuid();
    Db.Bills.Add(NewBill(billId, "2024-003"));
    await Db.SaveChangesAsync();

    _renderer.RenderAsync(billId, Arg.Any<CancellationToken>())
      .ThrowsAsync(new InvalidOperationException("chromium crashed"));

    await Should.NotThrowAsync(() =>
      CreateSut().Handle(new BillCreatedDomainEvent(billId), default));

    Bill? bill = await Db.Bills.AsNoTracking().FirstOrDefaultAsync(b => b.Id == billId);
    bill.ShouldNotBeNull();
    bill.DocumentContent.ShouldBeNull();
  }
}
