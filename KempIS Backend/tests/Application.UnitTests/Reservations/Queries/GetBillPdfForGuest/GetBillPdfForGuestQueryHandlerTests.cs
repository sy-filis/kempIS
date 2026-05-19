using Application.Finance.Bills;
using Application.Finance.Bills.GetBillPdf;
using Application.Reservations.Queries.GetBillPdfForGuest;
using Domain.Common;
using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Domain.Reservations;
using Domain.Reservations.ReservationStates;
using SharedKernel;
using TestUtilities.Builders;
using DomainReservation = Domain.Reservations.Reservations.Reservation;

namespace Application.UnitTests.Reservations.Queries.GetBillPdfForGuest;

public sealed class GetBillPdfForGuestQueryHandlerTests : HandlerTestBase
{
  private readonly IBillDocumentRenderer _renderer = Substitute.For<IBillDocumentRenderer>();

  private GetBillPdfForGuestQueryHandler CreateSut() => new(Db, _renderer, Clock);

  private static Address Addr() => new(Guid.NewGuid(), "Prague", "10000", "Main", "1");

  private async Task<DomainReservation> SeedReservation(string secret = "guest-secret")
  {
    DomainReservation r = new ReservationBuilder()
      .InState(ReservationState.Created)
      .WithSecret(secret)
      .Build();
    Db.Reservations.Add(r);
    await Db.SaveChangesAsync();
    return r;
  }

  private static Bill NewBill(Guid id, Guid? reservationId, string number) => new()
  {
    Id = id,
    Number = number,
    Kind = BillKind.Regular,
    ReservationId = reservationId,
    LanguageIdGuid = Guid.NewGuid(),
    IssuedAtUtc = DateTime.UtcNow,
    CheckInAt = new DateOnly(2026, 4, 20),
    CheckOutAt = new DateOnly(2026, 4, 22),
    Payer = new Payer { Name = "John", Surname = "Doe", Address = Addr() },
    LegalEntity = new LegalEntity { Name = "Acme", Cin = "1", Tin = "1", Address = Addr() },
    Payment = new Payment(PaymentType.Card, 100m),
  };

  [Fact]
  public async Task Handle_ReservationMissing_ReturnsReservationNotFound()
  {
    var missing = Guid.NewGuid();

    Result<GetBillPdfResponse> result = await CreateSut().Handle(
      new GetBillPdfForGuestQuery(missing, Guid.NewGuid(), "secret"), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.NotFound(missing));
    await _renderer.DidNotReceive().RenderAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_SecretMismatch_ReturnsSecretInvalid()
  {
    DomainReservation r = await SeedReservation(secret: "real");
    Bill bill = NewBill(Guid.NewGuid(), r.Id, "2026/0001");
    bill.DocumentContent = [1, 2, 3];
    Db.Bills.Add(bill);
    await Db.SaveChangesAsync();

    Result<GetBillPdfResponse> result = await CreateSut().Handle(
      new GetBillPdfForGuestQuery(r.Id, bill.Id, "wrong"), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.SecretInvalid);
    await _renderer.DidNotReceive().RenderAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_BillBelongsToOtherReservation_ReturnsBillNotFound()
  {
    DomainReservation r = await SeedReservation(secret: "correct");
    Bill foreignBill = NewBill(Guid.NewGuid(), Guid.NewGuid(), "FOREIGN");
    foreignBill.DocumentContent = [1, 2, 3];
    Db.Bills.Add(foreignBill);
    await Db.SaveChangesAsync();

    Result<GetBillPdfResponse> result = await CreateSut().Handle(
      new GetBillPdfForGuestQuery(r.Id, foreignBill.Id, "correct"), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Bill.NotFound");
  }

  [Fact]
  public async Task Handle_CachedDocument_ReturnsBytesWithoutRendering()
  {
    DomainReservation r = await SeedReservation(secret: "correct");
    Bill bill = NewBill(Guid.NewGuid(), r.Id, "2026/0001");
    bill.DocumentContent = [42, 43, 44];
    Db.Bills.Add(bill);
    await Db.SaveChangesAsync();

    Result<GetBillPdfResponse> result = await CreateSut().Handle(
      new GetBillPdfForGuestQuery(r.Id, bill.Id, "correct"), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Content.ShouldBe(new byte[] { 42, 43, 44 });
    result.Value.ContentType.ShouldBe("application/pdf");
    result.Value.FileName.ShouldBe("bill-2026/0001.pdf");
    await _renderer.DidNotReceive().RenderAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_NoCachedDocument_RendersAndPersists()
  {
    DomainReservation r = await SeedReservation(secret: "correct");
    Bill bill = NewBill(Guid.NewGuid(), r.Id, "2026/0002");
    Db.Bills.Add(bill);
    await Db.SaveChangesAsync();

    byte[] rendered = [9, 9, 9];
    _renderer.RenderAsync(bill.Id, Arg.Any<CancellationToken>())
      .Returns(Result.Success(new BillDocumentRenderResult(rendered, "application/pdf", "en-US")));

    Result<GetBillPdfResponse> result = await CreateSut().Handle(
      new GetBillPdfForGuestQuery(r.Id, bill.Id, "correct"), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Content.ShouldBe(rendered);

    Bill? persisted = await Db.Bills.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bill.Id);
    persisted.ShouldNotBeNull();
    persisted.DocumentContent.ShouldBe(rendered);
    persisted.DocumentGeneratedAtUtc.ShouldNotBeNull();
  }
}
