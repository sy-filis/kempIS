using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Finance.FinancialClosings.ListFinancialClosings;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

internal sealed class ListFinancialClosingsEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("financial-closings", async (
      DateOnly? from,
      DateOnly? to,
      IQueryHandler<ListFinancialClosingsQuery, IReadOnlyList<FinancialClosingSummary>> handler,
      CancellationToken cancellationToken) =>
    {
      Result<IReadOnlyList<FinancialClosingSummary>> result =
        await handler.Handle(new ListFinancialClosingsQuery(from, to), cancellationToken);

      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.Finance)
    .WithName("ListFinancialClosings")
    .WithSummary("List financial closings")
    .WithDescription("""
      Returns financial closing summaries ordered by close date (newest first). Each summary
      includes the sequential closing number, total amount, close timestamp, the count of
      bills attached to it, and the identifier of the user who created the closing
      (null for closings created before the field was introduced).

      **Behavior:** optional `from` / `to` filter on `ClosedAtUtc` (`from` matches the start
      of the day UTC, `to` the end of the day UTC). Results are not paginated.
      """)
    .Produces<IReadOnlyList<FinancialClosingSummary>>(StatusCodes.Status200OK)
    .HasRole(Roles.Receptionist, Roles.Accountant, Roles.Manager);
  }
}
