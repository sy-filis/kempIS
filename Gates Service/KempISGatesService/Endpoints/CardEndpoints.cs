using System.Diagnostics;
using KempISGatesService.Infrastructure;
using KempISGatesService.Models;
using KempISGatesService.Services;

namespace KempISGatesService.Endpoints;

public static class CardEndpoints
{
  // Handlers are async even though OleDb itself is sync-only: the awaits free the request thread
  // during the per-key semaphore wait and the retry delays, so a retry storm cannot pin Kestrel
  // workers.
  public static void MapCardEndpoints(this IEndpointRouteBuilder app)
  {
    app.MapPut("/api/v1/cards/{key:int}", async (int key, PutCardRequest request, CardService service) =>
    {
      if (key <= 0)
      {
        return Results.BadRequest("Card key must be a positive integer.");
      }

      if (!LegacyTime.CanRepresent(request.ValidTo))
      {
        return Results.BadRequest("The provided validTo value is out of supported range.");
      }

      string realName = request.RealName;
      string note = request.Note;

      if (realName.Length > PutCardRequest.MaxFieldLength)
      {
        return Results.BadRequest($"realName cannot exceed {PutCardRequest.MaxFieldLength} characters.");
      }

      if (note.Length > PutCardRequest.MaxFieldLength)
      {
        return Results.BadRequest($"note cannot exceed {PutCardRequest.MaxFieldLength} characters.");
      }

      CardWriteResult result = await service.UpsertCardAsync(key, request.ValidTo, realName, note);

      return result switch
      {
        CardWriteResult.Success => Results.Ok(),
        CardWriteResult.DatabaseError => Results.StatusCode(StatusCodes.Status500InternalServerError),
        _ => throw new UnreachableException($"Unexpected CardWriteResult from UpsertCard: {result}")
      };
    })
    .WithName("UpsertCard")
    .Produces(StatusCodes.Status200OK)
    .Produces<string>(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status500InternalServerError);

    app.MapDelete("/api/v1/cards/{key:int}", async (int key, CardService service) =>
    {
      if (key <= 0)
      {
        return Results.BadRequest("Card key must be a positive integer.");
      }

      CardWriteResult result = await service.DeleteCardAsync(key);

      return result switch
      {
        CardWriteResult.Success => Results.NoContent(),
        CardWriteResult.NotFound => Results.NotFound(),
        CardWriteResult.DatabaseError => Results.StatusCode(StatusCodes.Status500InternalServerError),
        _ => throw new UnreachableException($"Unexpected CardWriteResult from DeleteCard: {result}")
      };
    })
    .WithName("DeleteCard")
    .Produces(StatusCodes.Status204NoContent)
    .Produces<string>(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status404NotFound)
    .Produces(StatusCodes.Status500InternalServerError);
  }
}
