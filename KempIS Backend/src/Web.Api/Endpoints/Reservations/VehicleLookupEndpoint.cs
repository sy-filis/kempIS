using Application.Abstractions.Messaging;
using Application.Reservations.Vehicles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Reservations;

internal sealed class VehicleLookupEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost("vehicles/lookup", async (
      VehicleLookupRequest request,
      IQueryHandler<GetVehicleByPlateQuery, VehicleLookupResponse> handler,
      CancellationToken cancellationToken) =>
    {
      GetVehicleByPlateQuery query = new(request.LicencePlate);

      Result<VehicleLookupResponse> result = await handler.Handle(query, cancellationToken);

      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.Reservations)
    .WithName("LookupVehicleByPlate")
    .WithSummary("Look up a vehicle by licence plate")
    .WithDescription("""
      Returns the canonical (normalized) licence plate and the bill's `checkoutAt` date when
      the supplied plate matches a vehicle that is currently attached to a bill whose
      `CheckOutAt` has not passed.

      **Normalization:** the input is uppercased and stripped of every character outside
      `[A-Z0-9]`. Both the input and each candidate's stored `RegistrationNumber` go
      through the same transformation before comparison, so spaces, dashes, dots, and
      diacritics in either side are tolerated.

      **Errors:** `400` empty body, missing `licencePlate`, plate longer than 40 chars, or
      plate normalizes to an empty string. `404` no vehicle satisfies the match.
      """)
    .Produces<VehicleLookupResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .RequireAuthorization();
  }
}

internal sealed record VehicleLookupRequest(string LicencePlate);
