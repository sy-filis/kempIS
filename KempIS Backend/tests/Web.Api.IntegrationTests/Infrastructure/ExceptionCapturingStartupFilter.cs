using System.Collections.Concurrent;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Web.Api.IntegrationTests.Infrastructure;

internal sealed class ExceptionCapturingHandler(ConcurrentQueue<Exception> sink) : IExceptionHandler
{
  public async ValueTask<bool> TryHandleAsync(
    HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
  {
    sink.Enqueue(exception);

    var problemDetails = new ProblemDetails
    {
      Status = StatusCodes.Status500InternalServerError,
      Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1",
      Title = "Server failure",
    };

    httpContext.Response.StatusCode = problemDetails.Status.Value;
    await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
    return true;
  }
}
