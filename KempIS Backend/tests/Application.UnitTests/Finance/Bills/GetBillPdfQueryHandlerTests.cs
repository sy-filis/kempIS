using Application.Finance.Bills;
using Application.Finance.Bills.GetBillPdf;
using Domain.Common;
using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using SharedKernel;

namespace Application.UnitTests.Finance.Bills;

public sealed class GetBillPdfQueryHandlerTests : HandlerTestBase
{
  private readonly IBillDocumentRenderer _renderer = Substitute.For<IBillDocumentRenderer>();

  private GetBillPdfQueryHandler CreateSut() => new(Db, _renderer, Clock);

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
  public async Task Handle_ReturnsStoredDocument_WhenDocumentExists()
  {
    var billId = Guid.NewGuid();
    Bill bill = NewBill(billId, "2024-001");
    bill.DocumentContent = [1, 2, 3];
    Db.Bills.Add(bill);
    await Db.SaveChangesAsync();

    Result<GetBillPdfResponse> result = await CreateSut().Handle(
      new GetBillPdfQuery(billId), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Content.ShouldBe(new byte[] { 1, 2, 3 });
    result.Value.ContentType.ShouldBe("application/pdf");
    result.Value.FileName.ShouldBe("bill-2024-001.pdf");

    await _renderer.DidNotReceive().RenderAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_RendersAndPersists_WhenDocumentMissing()
  {
    var billId = Guid.NewGuid();
    Bill bill = NewBill(billId, "2024-002");
    Db.Bills.Add(bill);
    await Db.SaveChangesAsync();

    byte[] renderedBytes = [9, 9];
    _renderer.RenderAsync(billId, Arg.Any<CancellationToken>())
      .Returns(Result.Success(new BillDocumentRenderResult(renderedBytes, "application/pdf", "en-US")));

    Result<GetBillPdfResponse> result = await CreateSut().Handle(
      new GetBillPdfQuery(billId), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Content.ShouldBe(renderedBytes);
    result.Value.ContentType.ShouldBe("application/pdf");
    result.Value.FileName.ShouldBe("bill-2024-002.pdf");

    Bill? persisted = await Db.Bills
      .AsNoTracking()
      .FirstOrDefaultAsync(b => b.Id == billId);

    persisted.ShouldNotBeNull();
    persisted.DocumentContent.ShouldBe(renderedBytes);
    persisted.DocumentGeneratedAtUtc.ShouldNotBeNull();
  }

  [Fact]
  public async Task Handle_ReturnsNotFound_WhenBillMissing()
  {
    var missing = Guid.NewGuid();

    Result<GetBillPdfResponse> result = await CreateSut().Handle(
      new GetBillPdfQuery(missing), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Bill.NotFound");
  }

  [Fact]
  public async Task Handle_ReturnsRenderFailure_WhenRendererFails()
  {
    var billId = Guid.NewGuid();
    Bill bill = NewBill(billId, "2024-003");
    Db.Bills.Add(bill);
    await Db.SaveChangesAsync();

    _renderer.RenderAsync(billId, Arg.Any<CancellationToken>())
      .Returns(Result.Failure<BillDocumentRenderResult>(Error.Problem("Render.Failed", "boom")));

    Result<GetBillPdfResponse> result = await CreateSut().Handle(
      new GetBillPdfQuery(billId), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Render.Failed");
  }
}
