using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Reservations.Guests.Commands.SubmitGuestsToPolice;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Reservations;

internal sealed class ReportGuestsToPoliceEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost("guests/report-to-police", async (
      ICommandHandler<SubmitGuestsToPoliceCommand> handler,
      CancellationToken cancellationToken) =>
    {
      Result result = await handler.Handle(new SubmitGuestsToPoliceCommand(), cancellationToken);
      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithTags(Tags.Reservations)
    .WithName("ReportGuestsToPolice")
    .WithSummary("Submit unreported guests to the police register")
    .WithDescription("""
      Pushes every non-Czech, checked-in guest that has not yet been reported (or has been
      updated since their last report) to the Czech Ubyport register. Czech guests are excluded
      by law.

      **Side effects:** sends a batch to the configured Ubyport reporter and stamps each
      successfully reported guest with a `ReportedAt` timestamp. When there is nothing to
      report the call succeeds silently.

      **Errors:** `500` Ubyport submission failed (upstream rejected the batch, returned an
      authorization error, or was unavailable).
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status500InternalServerError)
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}
