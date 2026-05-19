using System.Net;
using System.Text.Json;
using Application.Abstractions.Gate;
using Infrastructure.ExternalServices.Gate;
using Infrastructure.UnitTests.TestDoubles;
using Shouldly;

namespace Infrastructure.UnitTests.ExternalServices.Gate;

public sealed class HttpGateClientTests
{
  private static HttpClient BuildClient(StubHttpMessageHandler handler) =>
    new(handler) { BaseAddress = new Uri("https://gate.local") };

  [Fact]
  public async Task PutCardAsync_BuildsCorrectUrlAndJsonBody()
  {
    HttpRequestMessage? captured = null;
    string? capturedBody = null;
    using var handler = new StubHttpMessageHandler(async (req, ct) =>
    {
      captured = req;
      capturedBody = req.Content is null ? null : await req.Content.ReadAsStringAsync(ct);
      return new HttpResponseMessage(HttpStatusCode.OK);
    });
    using HttpClient client = BuildClient(handler);
    var sut = new HttpGateClient(client);

    var payload = new GateCardPayload(
      new DateTimeOffset(2026, 8, 15, 23, 59, 59, TimeSpan.FromHours(2)),
      "Jan Novák",
      "extra key");

    await sut.PutCardAsync(12345UL, payload, CancellationToken.None);

    captured.ShouldNotBeNull();
    captured!.Method.ShouldBe(HttpMethod.Put);
    captured.RequestUri!.AbsoluteUri.ShouldBe("https://gate.local/api/v1/cards/12345");

    capturedBody.ShouldNotBeNull();
    using var doc = JsonDocument.Parse(capturedBody!);
    doc.RootElement.GetProperty("validTo").GetString().ShouldBe("2026-08-15T23:59:59+02:00");
    doc.RootElement.GetProperty("realName").GetString().ShouldBe("Jan Novák");
    doc.RootElement.GetProperty("note").GetString().ShouldBe("extra key");
  }

  [Fact]
  public async Task DeleteCardAsync_BuildsCorrectUrl()
  {
    HttpRequestMessage? captured = null;
    using var handler = new StubHttpMessageHandler((req, _) =>
    {
      captured = req;
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    });
    using HttpClient client = BuildClient(handler);
    var sut = new HttpGateClient(client);

    await sut.DeleteCardAsync(67890UL, CancellationToken.None);

    captured.ShouldNotBeNull();
    captured!.Method.ShouldBe(HttpMethod.Delete);
    captured.RequestUri!.AbsoluteUri.ShouldBe("https://gate.local/api/v1/cards/67890");
  }

  [Fact]
  public async Task PutCardAsync_NonSuccessStatus_Throws()
  {
    using var handler = new StubHttpMessageHandler(HttpStatusCode.InternalServerError, "{}");
    using HttpClient client = BuildClient(handler);
    var sut = new HttpGateClient(client);

    var payload = new GateCardPayload(DateTimeOffset.UtcNow, "x", "y");

    await Should.ThrowAsync<HttpRequestException>(() =>
      sut.PutCardAsync(1UL, payload, CancellationToken.None));
  }

  [Fact]
  public async Task DeleteCardAsync_NonSuccessStatus_Throws()
  {
    using var handler = new StubHttpMessageHandler(HttpStatusCode.InternalServerError, "{}");
    using HttpClient client = BuildClient(handler);
    var sut = new HttpGateClient(client);

    await Should.ThrowAsync<HttpRequestException>(() =>
      sut.DeleteCardAsync(1UL, CancellationToken.None));
  }
}
