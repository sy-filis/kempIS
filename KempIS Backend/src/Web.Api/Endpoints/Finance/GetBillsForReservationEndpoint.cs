using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Finance.Bills.GetBillsForReservation;
using Application.Finance.Bills.ListBills;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

internal sealed class GetBillsForReservationEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("reservations/{reservationId:guid}/bills", async (
      Guid reservationId,
      IQueryHandler<GetBillsForReservationQuery, IReadOnlyList<BillSummary>> handler,
      CancellationToken cancellationToken) =>
    {
      Result<IReadOnlyList<BillSummary>> result = await handler.Handle(new GetBillsForReservationQuery(reservationId), cancellationToken);
      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.Bills)
    .WithName("GetBillsForReservation")
    .WithSummary("List bills attached to a reservation")
    .WithDescription("""
      Returns every bill - regular or repair - whose `ReservationId` matches the supplied
      reservation, ordered by issue date (newest first). An unknown reservation id returns
      an empty list.
      """)
    .Produces<IReadOnlyList<BillSummary>>(StatusCodes.Status200OK)
    .HasRole(Roles.Receptionist, Roles.Manager, Roles.Accountant);
  }
}
