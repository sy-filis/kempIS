using Infrastructure.Database;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Web.Api.Infrastructure;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
  public async ValueTask<bool> TryHandleAsync(
      HttpContext httpContext,
      Exception exception,
      CancellationToken cancellationToken)
  {
    if (exception is BadHttpRequestException badRequest)
    {
      logger.LogWarning(badRequest, "Bad HTTP request: {Message}", badRequest.Message);

      ProblemDetails badRequestDetails = new()
      {
        Status = badRequest.StatusCode,
        Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
        Title = "Bad request",
        Detail = badRequest.Message
      };

      httpContext.Response.StatusCode = badRequestDetails.Status.Value;
      await httpContext.Response.WriteAsJsonAsync(badRequestDetails, cancellationToken);
      return true;
    }

    if (exception is DatabaseConstraintViolationException dbEx)
    {
      logger.LogWarning(dbEx, "Database constraint violation: {Kind} {Constraint}", dbEx.Kind, dbEx.ConstraintName);

      ProblemDetails conflict = new()
      {
        Status = StatusCodes.Status409Conflict,
        Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.8",
        Title = dbEx.Kind switch
        {
          ConstraintKind.ForeignKey => "Foreign key constraint violation",
          ConstraintKind.Unique => "Unique constraint violation",
          _ => "Database constraint violation"
        },
        Detail = dbEx.Detail
      };
      if (dbEx.ConstraintName is not null)
      {
        conflict.Extensions["constraint"] = dbEx.ConstraintName;
      }

      httpContext.Response.StatusCode = conflict.Status.Value;
      await httpContext.Response.WriteAsJsonAsync(conflict, cancellationToken);
      return true;
    }

    logger.LogError(exception, "Unhandled exception occurred");

    var problemDetails = new ProblemDetails
    {
      Status = StatusCodes.Status500InternalServerError,
      Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1",
      Title = "Server failure"
    };

    httpContext.Response.StatusCode = problemDetails.Status.Value;

    await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

    return true;
  }
}
