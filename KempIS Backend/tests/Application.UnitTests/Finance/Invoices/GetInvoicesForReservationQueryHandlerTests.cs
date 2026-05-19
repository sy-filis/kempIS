using Application.Finance.Invoices.GetInvoicesForReservation;
using Application.Finance.Invoices.ListInvoices;
using Domain.Common;
using Domain.Finance.Invoices;
using Domain.Finance.Payers;
using Domain.Reservations.Reservations;
using SharedKernel;
using TestUtilities.Builders;

namespace Application.UnitTests.Finance.Invoices;

public sealed class GetInvoicesForReservationQueryHandlerTests : HandlerTestBase
{
  private GetInvoicesForReservationQueryHandler CreateSut() => new(Db);

  private static Address Addr() => new(Guid.NewGuid(), "Prague", "10000", "Main", "1");

  [Fact]
  public async Task Handle_ReturnsOnlyInvoicesForReservation()
  {
    var targetPeriod = new DateRange(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 8));
    Reservation targetReservation = new ReservationBuilder().For(targetPeriod).WithNumber("R-2026/TARGET").Build();
    Reservation otherReservation = new ReservationBuilder().Build();
    Db.Reservations.Add(targetReservation);
    Db.Reservations.Add(otherReservation);

    Db.Invoices.Add(new Invoice
    {
      Id = Guid.NewGuid(),
      ReservationId = targetReservation.Id,
      Status = InvoiceStatus.Draft,
      IssuedAt = new DateOnly(2026, 4, 1),
      Email = "seed@example.com",
      PhoneNumber = "+420000000000",
      Payer = new Payer { Name = "A", Surname = "B", Address = Addr() },
    });
    Db.Invoices.Add(new Invoice
    {
      Id = Guid.NewGuid(),
      ReservationId = targetReservation.Id,
      Status = InvoiceStatus.Paid,
      Number = "N",
      IssuedAt = new DateOnly(2026, 4, 2),
      Email = "seed@example.com",
      PhoneNumber = "+420000000000",
      Payer = new Payer { Name = "A", Surname = "B", Address = Addr() },
    });
    Db.Invoices.Add(new Invoice
    {
      Id = Guid.NewGuid(),
      ReservationId = otherReservation.Id,
      Status = InvoiceStatus.Draft,
      IssuedAt = new DateOnly(2026, 4, 3),
      Email = "seed@example.com",
      PhoneNumber = "+420000000000",
      Payer = new Payer { Name = "A", Surname = "B", Address = Addr() },
    });
    await Db.SaveChangesAsync();

    Result<IReadOnlyList<InvoiceSummary>> result = await CreateSut()
      .Handle(new GetInvoicesForReservationQuery(targetReservation.Id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Count.ShouldBe(2);
    result.Value.ShouldAllBe(s => s.Reservation.Id == targetReservation.Id);
    result.Value.ShouldAllBe(s => s.Reservation.Number == "R-2026/TARGET");
    result.Value.ShouldAllBe(s => s.Reservation.From == targetPeriod.From && s.Reservation.To == targetPeriod.To);
  }
}
