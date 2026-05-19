using Application.Configuration;
using Application.Finance.Invoices.CreateInvoice;
using Application.Finance.Invoices.Shared;
using Domain.Common;
using Domain.Finance.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Application.UnitTests.Finance.Invoices;

public sealed class CreateInvoiceCommandHandlerTests : HandlerTestBase
{
  private readonly RetentionSettings _retentionSettings = new()
  {
    GuestYears = 6,
    BillYears = 10,
    InvoiceYears = 10,
    RunAtLocalTime = new TimeOnly(3, 0),
  };

  private CreateInvoiceCommandHandler CreateSut() =>
    new(Db, Clock, Options.Create(_retentionSettings));

  private static Address Addr() => new(Guid.NewGuid(), "Prague", "10000", "Main", "1");

  private static InvoicePayerInput PayerInput() => new("John", "Doe", Addr());

  private static InvoiceLegalEntityInput LegalEntityInput() =>
    new("Acme s.r.o.", "12345678", "CZ12345678", Addr());

  private static readonly Guid ServiceId = Guid.NewGuid();

  private static CreateInvoiceCommand WithPayer(Guid reservationId) => new(
    reservationId,
    PayerInput(),
    LegalEntity: null,
    Email: "billing@example.com",
    PhoneNumber: "+420123456789",
    Items: [new InvoiceItemInput(ServiceId, 2m, 500m, 21m)]);

  private static CreateInvoiceCommand WithLegalEntity(Guid reservationId) => new(
    reservationId,
    Payer: null,
    LegalEntityInput(),
    Email: "billing@example.com",
    PhoneNumber: "+420123456789",
    Items: [new InvoiceItemInput(ServiceId, 2m, 500m, 21m)]);

  [Fact]
  public async Task Handle_PersistsInvoice_InDraftStatus_WithNullNumber()
  {
    var reservationId = Guid.NewGuid();

    Result<CreateInvoiceResponse> result =
      await CreateSut().Handle(WithPayer(reservationId), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();

    Invoice? stored = await Db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == result.Value.InvoiceId);
    stored.ShouldNotBeNull();
    stored!.Status.ShouldBe(InvoiceStatus.Draft);
    stored.Number.ShouldBeNull();
    stored.ReservationId.ShouldBe(reservationId);
    stored.LinkedBillId.ShouldBeNull();
    stored.PaidAt.ShouldBeNull();
    stored.IssuedAt.ShouldBe(DateOnly.FromDateTime(Clock.UtcNow));
    stored.Email.ShouldBe("billing@example.com");
    stored.PhoneNumber.ShouldBe("+420123456789");
  }

  [Fact]
  public async Task Handle_PersistsInvoiceItems_Verbatim()
  {
    var reservationId = Guid.NewGuid();

    Result<CreateInvoiceResponse> result =
      await CreateSut().Handle(WithPayer(reservationId), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();

    List<Domain.Finance.InvoiceItems.InvoiceItem> items =
      [.. Db.InvoiceItems.Where(i => i.InvoiceId == result.Value.InvoiceId)];

    items.Count.ShouldBe(1);
    items[0].ServiceGuid.ShouldBe(ServiceId);
    items[0].Quantity.ShouldBe(2m);
    items[0].UnitPrice.ShouldBe(500m);
    items[0].VatRatePercentage.ShouldBe(21m);
  }

  [Fact]
  public async Task Handle_PersistsPayerOnlyInvoice_WithoutLegalEntity()
  {
    Result<CreateInvoiceResponse> result =
      await CreateSut().Handle(WithPayer(Guid.NewGuid()), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();

    Invoice stored = (await Db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == result.Value.InvoiceId))!;
    stored.Payer.ShouldNotBeNull();
    stored.Payer!.Name.ShouldBe("John");
    stored.LegalEntity.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_PersistsLegalEntityOnlyInvoice_WithoutPayer()
  {
    Result<CreateInvoiceResponse> result =
      await CreateSut().Handle(WithLegalEntity(Guid.NewGuid()), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();

    Invoice stored = (await Db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == result.Value.InvoiceId))!;
    stored.LegalEntity.ShouldNotBeNull();
    stored.LegalEntity!.Name.ShouldBe("Acme s.r.o.");
    stored.Payer.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_PersistsDueTo_WhenSupplied()
  {
    var due = new DateOnly(2026, 6, 1);
    CreateInvoiceCommand command = WithPayer(Guid.NewGuid()) with { DueTo = due };

    Result<CreateInvoiceResponse> result =
      await CreateSut().Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();

    Invoice stored = (await Db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == result.Value.InvoiceId))!;
    stored.DueTo.ShouldBe(due);
  }

  [Fact]
  public async Task Handle_LeavesDueToNull_WhenOmitted()
  {
    Result<CreateInvoiceResponse> result =
      await CreateSut().Handle(WithPayer(Guid.NewGuid()), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();

    Invoice stored = (await Db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == result.Value.InvoiceId))!;
    stored.DueTo.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_SetsScartationFromConfig()
  {
    var reservationId = Guid.NewGuid();

    Result<CreateInvoiceResponse> result =
      await CreateSut().Handle(WithPayer(reservationId), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Invoice stored = (await Db.Invoices.FindAsync(result.Value.InvoiceId))!;
    stored.Scartation.ShouldBe(stored.IssuedAt.AddYears(_retentionSettings.InvoiceYears));
  }
}
