using Application.Finance.Bills;
using Application.Finance.Bills.BillSticker;
using Domain.Common;
using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using SharedKernel;

namespace Application.UnitTests.Finance.Bills;

public sealed class GetBillStickerQueryHandlerTests : HandlerTestBase
{
  private readonly IBillStickerRenderer _renderer = Substitute.For<IBillStickerRenderer>();

  private GetBillStickerQueryHandler CreateSut() => new(Db, _renderer);

  [Fact]
  public async Task Handle_ReturnsBytes_WhenRendererSucceeds()
  {
    var billId = Guid.NewGuid();
    Db.Bills.Add(NewBill(billId, "B-123"));
    await Db.SaveChangesAsync();

    _renderer.RenderAsync(Arg.Any<Bill>(), Arg.Any<CancellationToken>())
      .Returns(Result.Success<byte[]>([0xAA, 0xBB]));

    Result<GetBillStickerResponse> result = await CreateSut().Handle(
      new GetBillStickerQuery(billId), default);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Content.ShouldBe([0xAA, 0xBB]);
    result.Value.ContentType.ShouldBe("application/pdf");
    result.Value.FileName.ShouldBe("bill-sticker-B-123.pdf");
  }

  [Fact]
  public async Task Handle_ReturnsNotFound_WhenBillMissing()
  {
    Result<GetBillStickerResponse> result = await CreateSut().Handle(
      new GetBillStickerQuery(Guid.NewGuid()), default);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Bill.NotFound");
    await _renderer.DidNotReceiveWithAnyArgs().RenderAsync(default!, default);
  }

  [Fact]
  public async Task Handle_ReturnsRendererFailure_WhenRendererFails()
  {
    var billId = Guid.NewGuid();
    Db.Bills.Add(NewBill(billId, "B-FAIL"));
    await Db.SaveChangesAsync();

    _renderer.RenderAsync(Arg.Any<Bill>(), Arg.Any<CancellationToken>())
      .Returns(Result.Failure<byte[]>(Error.Problem("Render.Failed", "boom")));

    Result<GetBillStickerResponse> result = await CreateSut().Handle(
      new GetBillStickerQuery(billId), default);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Render.Failed");
  }

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
}
