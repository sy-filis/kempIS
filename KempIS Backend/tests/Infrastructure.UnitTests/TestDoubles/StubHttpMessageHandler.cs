using System.Net;
using System.Text;

namespace Infrastructure.UnitTests.TestDoubles;

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
  private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

  public Uri? LastRequestUri { get; private set; }

  public StubHttpMessageHandler(HttpStatusCode statusCode, string json)
    : this((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)
    {
      Content = new StringContent(json, Encoding.UTF8, "application/json"),
    }))
  {
  }

  public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
  {
    _handler = handler;
  }

  public static StubHttpMessageHandler Throwing(Exception exception) =>
    new((_, _) => Task.FromException<HttpResponseMessage>(exception));

  protected override Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage request,
    CancellationToken cancellationToken)
  {
    LastRequestUri = request.RequestUri;
    return _handler(request, cancellationToken);
  }
}
