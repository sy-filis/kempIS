using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Finance.FinancialClosings.CreateFinancialClosing;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

internal sealed class CreateFinancialClosingEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost("financial-closings", async (
      ICommandHandler<CreateFinancialClosingCommand, CreateFinancialClosingResponse> handler,
      CancellationToken cancellationToken) =>
    {
      Result<CreateFinancialClosingResponse> result =
        await handler.Handle(new CreateFinancialClosingCommand(), cancellationToken);

      return result.Match(
        response => Results.Created($"/financial-closings/{response.Id}", response),
        CustomResults.Problem);
    })
    .WithTags(Tags.Finance)
    .WithName("CreateFinancialClosing")
    .WithSummary("Create a financial closing for the open bills")
    .WithDescription("""
      Closes every bill that is not yet part of a financial closing into a new closing record,
      assigns it a sequential closing number, and renders and persists the closing report PDF.

      **Behavior:** the closing snapshots the count and total amount of currently-open bills,
      stamps them with the new closing id in a single bulk update, and then renders the
      report. The sequential closing id is computed as `max + 1` over existing closings;
      callers should not invoke this concurrently for the same closing window - retry on a
      `500` response if a concurrent caller raced ahead.

      **Errors:** `409` no open bills are eligible for closing - every existing bill is
      already part of a prior closing. `500` the report renderer failed, or a concurrent
      closing call won the race for the next sequential id (retry the request).
      """)
    .Produces<CreateFinancialClosingResponse>(StatusCodes.Status201Created)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .ProducesProblem(StatusCodes.Status500InternalServerError)
    .HasRole(Roles.Receptionist, Roles.Accountant, Roles.Manager);
  }
}
