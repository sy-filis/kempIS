using System.Net;

namespace Application.UnitTests.Infrastructure.EDoklady;

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
  private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responders = new();

  public List<HttpRequestMessage> Received { get; } = [];

  public List<string?> ReceivedBodies { get; } = [];

  public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
      _responders.Enqueue(responder);

  public void EnqueueJson(HttpStatusCode status, string jsonBody) =>
      _responders.Enqueue(_ => new HttpResponseMessage(status)
      {
        Content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json"),
      });

  public void EnqueueStatus(HttpStatusCode status, string? body = null) =>
      _responders.Enqueue(_ => new HttpResponseMessage(status)
      {
        Content = body is null ? null! : new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
      });

  public void EnqueueThrow(Exception ex) =>
      _responders.Enqueue(_ => throw ex);

  protected override async Task<HttpResponseMessage> SendAsync(
      HttpRequestMessage request,
      CancellationToken cancellationToken)
  {
    Received.Add(request);
    ReceivedBodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));

    if (_responders.Count == 0)
    {
      throw new InvalidOperationException($"No responder enqueued for {request.Method} {request.RequestUri}");
    }

    return _responders.Dequeue()(request);
  }
}
