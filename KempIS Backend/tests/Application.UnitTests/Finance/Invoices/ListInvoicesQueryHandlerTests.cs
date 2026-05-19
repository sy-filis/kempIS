using Application.Finance.Invoices.ListInvoices;
using Domain.Common;
using Domain.Finance.InvoiceItems;
using Domain.Finance.Invoices;
using Domain.Finance.Payers;
using Domain.Reservations.Reservations;
using SharedKernel;
using TestUtilities.Builders;
using TestUtilities.Fakes;

namespace Application.UnitTests.Finance.Invoices;

public sealed class ListInvoicesQueryHandlerTests : HandlerTestBase
{
  private ListInvoicesQueryHandler CreateSut() => new(Db, Clock);

  private static Address Addr() => new(Guid.NewGuid(), "Prague", "10000", "Main", "1");

  private Guid SeedReservation()
  {
    Reservation reservation = new ReservationBuilder().Build();
    Db.Reservations.Add(reservation);
    return reservation.Id;
  }

  private Guid SeedReservation(DateRange period)
  {
    Reservation reservation = new ReservationBuilder().For(period).Build();
    Db.Reservations.Add(reservation);
    return reservation.Id;
  }

  private static Invoice MakeInvoice(
    Guid id,
    Guid reservationId,
    InvoiceStatus status,
    DateTime issuedAt,
    string? number = null,
    DateTime? dueToUtc = null) =>
    new()
    {
      Id = id,
      ReservationId = reservationId,
      Status = status,
      Number = number,
      IssuedAt = DateOnly.FromDateTime(issuedAt),
      DueTo = dueToUtc.HasValue ? DateOnly.FromDateTime(dueToUtc.Value) : null,
      Email = "seed@example.com",
      PhoneNumber = "+420000000000",
      Payer = new Payer { Name = "A", Surname = "B", Address = Addr() },
    };

  [Fact]
  public async Task Handle_ReturnsAllInvoices_WhenNoFilters()
  {
    Db.Invoices.Add(MakeInvoice(Guid.NewGuid(), SeedReservation(), InvoiceStatus.Draft, new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)));
    Db.Invoices.Add(MakeInvoice(Guid.NewGuid(), SeedReservation(), InvoiceStatus.Created, new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc), "N1"));
    Db.Invoices.Add(MakeInvoice(Guid.NewGuid(), SeedReservation(), InvoiceStatus.Paid, new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc), "N2"));
    await Db.SaveChangesAsync();

    Result<IReadOnlyList<InvoiceSummary>> result = await CreateSut()
      .Handle(new ListInvoicesQuery(null, null, null, null), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Count.ShouldBe(3);
    result.Value[0].IssuedAt.ShouldBe(new DateOnly(2026, 4, 10));
  }

  [Fact]
  public async Task Handle_FiltersByReservationId()
  {
    var targetPeriod = new DateRange(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 7));
    Reservation targetReservationEntity = new ReservationBuilder().For(targetPeriod).WithNumber("R-2026/TARGET").Build();
    Db.Reservations.Add(targetReservationEntity);

    Db.Invoices.Add(MakeInvoice(Guid.NewGuid(), targetReservationEntity.Id, InvoiceStatus.Draft, new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)));
    Db.Invoices.Add(MakeInvoice(Guid.NewGuid(), SeedReservation(), InvoiceStatus.Draft, new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc)));
    await Db.SaveChangesAsync();

    Result<IReadOnlyList<InvoiceSummary>> result = await CreateSut()
      .Handle(new ListInvoicesQuery(null, null, targetReservationEntity.Id, null), CancellationToken.None);

    result.Value.Count.ShouldBe(1);
    result.Value[0].Reservation.Id.ShouldBe(targetReservationEntity.Id);
    result.Value[0].Reservation.Number.ShouldBe("R-2026/TARGET");
    result.Value[0].Reservation.From.ShouldBe(targetPeriod.From);
    result.Value[0].Reservation.To.ShouldBe(targetPeriod.To);
  }

  [Fact]
  public async Task Handle_FiltersByState_Created()
  {
    Db.Invoices.Add(MakeInvoice(Guid.NewGuid(), SeedReservation(), InvoiceStatus.Draft, new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)));
    Db.Invoices.Add(MakeInvoice(Guid.NewGuid(), SeedReservation(), InvoiceStatus.Created, new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc), "N1"));
    await Db.SaveChangesAsync();

    Result<IReadOnlyList<InvoiceSummary>> result = await CreateSut()
      .Handle(new ListInvoicesQuery(null, null, null, InvoiceStateFilter.Created), CancellationToken.None);

    result.Value.Count.ShouldBe(1);
    result.Value[0].Status.ShouldBe(InvoiceStatus.Created);
  }

  [Fact]
  public async Task Handle_FiltersByState_AfterDue_MatchesOnlyCreatedWithPastDue()
  {
    DateTime now = FakeDateTimeProvider.DefaultUtc;

    Db.Invoices.Add(MakeInvoice(Guid.NewGuid(), SeedReservation(), InvoiceStatus.Created, now.AddDays(-30), "OVERDUE", dueToUtc: now.AddDays(-1)));
    Db.Invoices.Add(MakeInvoice(Guid.NewGuid(), SeedReservation(), InvoiceStatus.Created, now.AddDays(-30), "FUTURE", dueToUtc: now.AddDays(7)));
    Db.Invoices.Add(MakeInvoice(Guid.NewGuid(), SeedReservation(), InvoiceStatus.Created, now.AddDays(-30), "NODUE", dueToUtc: null));
    Db.Invoices.Add(MakeInvoice(Guid.NewGuid(), SeedReservation(), InvoiceStatus.Draft, now.AddDays(-30), dueToUtc: now.AddDays(-1)));
    Db.Invoices.Add(MakeInvoice(Guid.NewGuid(), SeedReservation(), InvoiceStatus.Paid, now.AddDays(-30), "PAID", dueToUtc: now.AddDays(-1)));
    await Db.SaveChangesAsync();

    Result<IReadOnlyList<InvoiceSummary>> result = await CreateSut()
      .Handle(new ListInvoicesQuery(null, null, null, InvoiceStateFilter.AfterDue), CancellationToken.None);

    result.Value.Count.ShouldBe(1);
    result.Value[0].Number.ShouldBe("OVERDUE");
  }

  [Fact]
  public async Task Handle_FiltersByReservationPeriodOverlap()
  {
    Guid insideOnly = SeedReservation(new DateRange(new DateOnly(2026, 4, 5), new DateOnly(2026, 4, 10)));
    Guid leftEdge = SeedReservation(new DateRange(new DateOnly(2026, 3, 20), new DateOnly(2026, 4, 1)));
    Guid rightEdge = SeedReservation(new DateRange(new DateOnly(2026, 4, 30), new DateOnly(2026, 5, 5)));
    Guid before = SeedReservation(new DateRange(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 10)));
    Guid after = SeedReservation(new DateRange(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 10)));

    var t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    Db.Invoices.Add(MakeInvoice(Guid.NewGuid(), insideOnly, InvoiceStatus.Draft, t));
    Db.Invoices.Add(MakeInvoice(Guid.NewGuid(), leftEdge, InvoiceStatus.Draft, t));
    Db.Invoices.Add(MakeInvoice(Guid.NewGuid(), rightEdge, InvoiceStatus.Draft, t));
    Db.Invoices.Add(MakeInvoice(Guid.NewGuid(), before, InvoiceStatus.Draft, t));
    Db.Invoices.Add(MakeInvoice(Guid.NewGuid(), after, InvoiceStatus.Draft, t));
    await Db.SaveChangesAsync();

    var from = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    var to = new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc);
    Result<IReadOnlyList<InvoiceSummary>> result = await CreateSut()
      .Handle(new ListInvoicesQuery(from, to, null, null), CancellationToken.None);

    HashSet<Guid> matched = [.. result.Value.Select(s => s.Reservation.Id)];
    matched.ShouldBe(new[] { insideOnly, leftEdge, rightEdge }, ignoreOrder: true);
  }

  [Fact]
  public async Task Handle_ComputesTotalAmount_FromItemsAsGrossSum()
  {
    var invoiceId = Guid.NewGuid();
    Db.Invoices.Add(MakeInvoice(invoiceId, SeedReservation(), InvoiceStatus.Paid, new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), "N"));
    // UnitPrice is VAT-inclusive (gross). TotalAmount is the plain sum of qty × UnitPrice.
    Db.InvoiceItems.Add(new InvoiceItem { Id = Guid.NewGuid(), InvoiceId = invoiceId, ServiceGuid = Guid.NewGuid(), Quantity = 2m, UnitPrice = 500m, VatRatePercentage = 21m });
    Db.InvoiceItems.Add(new InvoiceItem { Id = Guid.NewGuid(), InvoiceId = invoiceId, ServiceGuid = Guid.NewGuid(), Quantity = 1m, UnitPrice = 100m, VatRatePercentage = 21m });
    await Db.SaveChangesAsync();

    Result<IReadOnlyList<InvoiceSummary>> result = await CreateSut()
      .Handle(new ListInvoicesQuery(null, null, null, null), CancellationToken.None);

    result.Value.Count.ShouldBe(1);
    result.Value[0].TotalAmount.ShouldBe(1100m);  // 2*500 + 1*100 = 1100
  }
}
