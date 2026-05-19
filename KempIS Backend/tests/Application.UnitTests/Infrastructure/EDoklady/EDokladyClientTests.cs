using System.Net;
using System.Text.Json;
using Application.Abstractions.EDoklady;
using Infrastructure.ExternalServices.EDoklady;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Application.UnitTests.Infrastructure.EDoklady;

public sealed class EDokladyClientTests : IDisposable
{
  private readonly StubHttpMessageHandler _handler = new();
  private readonly FakeDateTimeProvider _clock = new(new DateTime(2026, 4, 21, 12, 0, 0, DateTimeKind.Utc));
  private readonly EDokladyOptions _options = new()
  {
    BaseUrl = "https://edoklady.test/",
    Certificate = new EDokladyOptions.CertificateOptions { PfxPath = "x", PfxPassword = "y" },
    QrCodeRefreshThresholdDays = 7,
    Geolocation = new EDokladyOptions.GeolocationOptions
    {
      Latitude = 50.08,
      Longitude = 14.42,
      ToleranceInMeters = 20,
    },
  };
  private HttpClient? _httpClient;

  private EDokladyClient CreateSut()
  {
    _httpClient = new HttpClient(_handler) { BaseAddress = new Uri(_options.BaseUrl) };
    return new EDokladyClient(_httpClient, Options.Create(_options), _clock);
  }

  public void Dispose()
  {
    _httpClient?.Dispose();
    _handler.Dispose();
  }

  [Fact]
  public async Task CreateVirtualServiceCounter_Returns_Mapped_Counter()
  {
    _handler.EnqueueJson(HttpStatusCode.Created, """
      {
        "id": "vsc-1",
        "name": "Recepce",
        "qrCode": { "data": "QR-DATA", "validTo": "2026-05-21T12:00:00Z" },
        "geolocation": { "latitude": 50.08, "longitude": 14.42, "toleranceInMeters": 20 }
      }
      """);

    EDokladyClient sut = CreateSut();

    Result<VirtualServiceCounter> result = await sut.CreateVirtualServiceCounterAsync(
        new CreateVirtualServiceCounterRequest("Recepce"),
        CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Id.ShouldBe("vsc-1");
    result.Value.Name.ShouldBe("Recepce");
    result.Value.QrCode.Data.ShouldBe("QR-DATA");
    result.Value.QrCode.ValidTo.ShouldBe(new DateTime(2026, 5, 21, 12, 0, 0, DateTimeKind.Utc));
    result.Value.Geolocation!.Latitude.ShouldBe(50.08);
    _handler.Received[0].Method.ShouldBe(HttpMethod.Post);
    _handler.Received[0].RequestUri!.PathAndQuery.ShouldBe("/integration/virtualServiceCounters");

    using var body = JsonDocument.Parse(_handler.ReceivedBodies[0]!);
    body.RootElement.GetProperty("name").GetString().ShouldBe("Recepce");
    body.RootElement.GetProperty("geolocation").GetProperty("latitude").GetDouble().ShouldBe(50.08);
  }

  [Fact]
  public async Task GetVirtualServiceCounter_QrValidBeyondThreshold_DoesNotRegenerate()
  {
    DateTime validTo = _clock.UtcNow.AddDays(8);
    _handler.EnqueueJson(HttpStatusCode.OK, $$"""
      {
        "id": "vsc-1",
        "name": "Recepce",
        "qrCode": { "data": "ORIGINAL-QR", "validTo": "{{validTo:O}}" }
      }
      """);

    EDokladyClient sut = CreateSut();

    Result<VirtualServiceCounter> result = await sut.GetVirtualServiceCounterAsync("vsc-1", CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.QrCode.Data.ShouldBe("ORIGINAL-QR");
    _handler.Received.Count.ShouldBe(1);
    _handler.Received[0].Method.ShouldBe(HttpMethod.Get);
    _handler.Received[0].RequestUri!.PathAndQuery.ShouldBe("/integration/virtualServiceCounters/vsc-1");
  }

  [Fact]
  public async Task GetVirtualServiceCounter_QrNearExpiry_Regenerates()
  {
    DateTime validTo = _clock.UtcNow.AddDays(3);
    _handler.EnqueueJson(HttpStatusCode.OK, $$"""
      {
        "id": "vsc-1",
        "qrCode": { "data": "ORIGINAL-QR", "validTo": "{{validTo:O}}" }
      }
      """);
    DateTime newValidTo = _clock.UtcNow.AddDays(30);
    _handler.EnqueueJson(HttpStatusCode.OK, $$"""
      { "data": "FRESH-QR", "validTo": "{{newValidTo:O}}" }
      """);

    EDokladyClient sut = CreateSut();

    Result<VirtualServiceCounter> result = await sut.GetVirtualServiceCounterAsync("vsc-1", CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.QrCode.Data.ShouldBe("FRESH-QR");
    result.Value.QrCode.ValidTo.ShouldBe(newValidTo);
    _handler.Received.Count.ShouldBe(2);
    _handler.Received[1].Method.ShouldBe(HttpMethod.Post);
    _handler.Received[1].RequestUri!.PathAndQuery.ShouldBe("/integration/virtualServiceCounters/vsc-1/generateQrCode");
  }

  [Fact]
  public async Task GetVirtualServiceCounter_QrAlreadyExpired_Regenerates()
  {
    DateTime validTo = _clock.UtcNow.AddHours(-1);
    _handler.EnqueueJson(HttpStatusCode.OK, $$"""
      { "id": "vsc-1", "qrCode": { "data": "STALE", "validTo": "{{validTo:O}}" } }
      """);
    DateTime newValidTo = _clock.UtcNow.AddDays(30);
    _handler.EnqueueJson(HttpStatusCode.OK, $$"""
      { "data": "FRESH-QR", "validTo": "{{newValidTo:O}}" }
      """);

    EDokladyClient sut = CreateSut();

    Result<VirtualServiceCounter> result = await sut.GetVirtualServiceCounterAsync("vsc-1", CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.QrCode.Data.ShouldBe("FRESH-QR");
    _handler.Received.Count.ShouldBe(2);
  }

  [Fact]
  public async Task StartPresentation_SendsFixedAttributeList_ReturnsTransactionId()
  {
    _handler.EnqueueJson(HttpStatusCode.Created, """
      { "transactionId": "tx-42" }
      """);

    EDokladyClient sut = CreateSut();

    Result<string> result = await sut.StartPresentationAsync("vsc-1", CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBe("tx-42");

    _handler.Received[0].Method.ShouldBe(HttpMethod.Post);
    _handler.Received[0].RequestUri!.PathAndQuery.ShouldBe("/integration/presentation/server/requestPresentation");

    using var body = JsonDocument.Parse(_handler.ReceivedBodies[0]!);
    body.RootElement.GetProperty("virtualServiceCounterId").GetString().ShouldBe("vsc-1");
    JsonElement docs = body.RootElement.GetProperty("requestDocuments");
    docs.GetArrayLength().ShouldBe(1);
    JsonElement first = docs[0];
    first.GetProperty("documentName").GetString().ShouldBe("org.iso.18013.5.1.CZ.mID");
    JsonElement attrs = first.GetProperty("attributes");
    attrs.GetArrayLength().ShouldBe(11);

    string[] expectedNames =
    [
      "portrait", "given_name", "family_name", "birth_date", "nationality",
      "document_number", "expiry_date", "resident_city", "resident_part_of_city",
      "resident_street", "resident_city_house_number",
    ];
    bool[] expectedRetain = [false, true, true, true, true, true, false, true, true, true, true];

    for (int i = 0; i < expectedNames.Length; i++)
    {
      attrs[i].GetProperty("name").GetString().ShouldBe(expectedNames[i]);
      attrs[i].GetProperty("intentToRetain").GetBoolean().ShouldBe(expectedRetain[i]);
    }
  }

  [Theory]
  [InlineData("WAITING_FOR_RESPONSE", TransactionStateKind.WaitingForResponse)]
  [InlineData("RESPONSE_RECEIVED", TransactionStateKind.ResponseReceived)]
  [InlineData("TIMEOUT", TransactionStateKind.Timeout)]
  public async Task GetTransaction_MapsWireState_ToPublicEnum(string wire, TransactionStateKind expected)
  {
    _handler.EnqueueJson(HttpStatusCode.OK, $$"""
      { "id": "tx-1", "state": "{{wire}}", "validTo": "2026-04-21T13:00:00Z" }
      """);

    EDokladyClient sut = CreateSut();

    Result<TransactionState> result = await sut.GetTransactionAsync("tx-1", CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Id.ShouldBe("tx-1");
    result.Value.State.ShouldBe(expected);
    result.Value.ValidTo.ShouldBe(new DateTime(2026, 4, 21, 13, 0, 0, DateTimeKind.Utc));
    _handler.Received[0].Method.ShouldBe(HttpMethod.Get);
    _handler.Received[0].RequestUri!.PathAndQuery.ShouldBe("/integration/presentation/server/transactions/tx-1");
  }

  [Fact]
  public async Task GetTransactionResult_MapsObtainedAndMissingCredentials_AndPropagatesQueryFlags()
  {
    _handler.EnqueueJson(HttpStatusCode.OK, """
      {
        "presentationResult": "SUCCESS",
        "obtainedDocuments": [
          {
            "documentName": "org.iso.18013.5.1.CZ.mID",
            "obtainedCredentials": [
              { "name": "given_name", "attributeDataType": "STRING", "value": "Jan" },
              { "name": "portrait", "attributeDataType": "PHOTO", "value": "base64..." }
            ],
            "missingCredentials": [
              { "name": "expiry_date", "attributeDataType": "DATE" }
            ],
            "MDoc": "mdoc-bytes"
          }
        ]
      }
      """);

    EDokladyClient sut = CreateSut();

    Result<TransactionResult> result = await sut.GetTransactionResultAsync(
        "tx-42", includeMDoc: true, includeMissingCredentials: true, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Outcome.ShouldBe(PresentationOutcome.Success);
    result.Value.Documents.Count.ShouldBe(1);

    PresentedDocument doc = result.Value.Documents[0];
    doc.DocumentName.ShouldBe("org.iso.18013.5.1.CZ.mID");
    doc.Obtained.Count.ShouldBe(2);
    doc.Obtained[0].Name.ShouldBe("given_name");
    doc.Obtained[0].DataType.ShouldBe(AttributeDataType.String);
    doc.Obtained[0].Value.ShouldBe("Jan");
    doc.Obtained[1].DataType.ShouldBe(AttributeDataType.Photo);
    doc.Missing!.Count.ShouldBe(1);
    doc.Missing[0].Name.ShouldBe("expiry_date");
    doc.Missing[0].DataType.ShouldBe(AttributeDataType.Date);
    doc.MDoc.ShouldBe("mdoc-bytes");

    _handler.Received[0].Method.ShouldBe(HttpMethod.Get);
    _handler.Received[0].RequestUri!.PathAndQuery
      .ShouldBe("/integration/presentation/server/transactions/tx-42/result?includeMDoc=true&includeMissingCredentials=true");
  }

  [Fact]
  public async Task GetTransactionResult_OmittedObtainedDocuments_ReturnsEmptyList()
  {
    _handler.EnqueueJson(HttpStatusCode.OK, """
      { "presentationResult": "MISSING_DATA" }
      """);

    EDokladyClient sut = CreateSut();

    Result<TransactionResult> result = await sut.GetTransactionResultAsync(
        "tx-1", includeMDoc: false, includeMissingCredentials: false, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Outcome.ShouldBe(PresentationOutcome.MissingData);
    result.Value.Documents.ShouldBeEmpty();
    _handler.Received[0].RequestUri!.PathAndQuery
      .ShouldBe("/integration/presentation/server/transactions/tx-1/result?includeMDoc=false&includeMissingCredentials=false");
  }

  [Fact]
  public async Task NetworkError_Returns_Unavailable()
  {
    _handler.EnqueueThrow(new HttpRequestException("boom"));
    EDokladyClient sut = CreateSut();

    Result<VirtualServiceCounter> result = await sut.GetVirtualServiceCounterAsync("x", CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe(EDokladyErrors.Unavailable.Code);
  }

  [Fact]
  public async Task ClientTimeout_Returns_Unavailable()
  {
    _handler.EnqueueThrow(new TaskCanceledException("timeout"));
    EDokladyClient sut = CreateSut();

    Result<VirtualServiceCounter> result = await sut.GetVirtualServiceCounterAsync("x", CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe(EDokladyErrors.Unavailable.Code);
  }

  [Fact]
  public async Task Http404_Returns_NotFound()
  {
    _handler.EnqueueStatus(HttpStatusCode.NotFound);
    EDokladyClient sut = CreateSut();

    Result<VirtualServiceCounter> result = await sut.GetVirtualServiceCounterAsync("missing", CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("EDoklady.NotFound");
    result.Error.Type.ShouldBe(ErrorType.NotFound);
  }

  [Fact]
  public async Task Http400_ReturnsBadRequest_WithJoinedErrors()
  {
    _handler.EnqueueStatus(HttpStatusCode.BadRequest, """{ "errors": ["first", "second"] }""");
    EDokladyClient sut = CreateSut();

    Result<string> result = await sut.StartPresentationAsync("vsc-1", CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("EDoklady.BadRequest");
    result.Error.Type.ShouldBe(ErrorType.Problem);
    result.Error.Description.ShouldBe("first; second");
  }

  [Fact]
  public async Task Http401_Returns_Rejected()
  {
    _handler.EnqueueStatus(HttpStatusCode.Unauthorized);
    EDokladyClient sut = CreateSut();

    Result<string> result = await sut.StartPresentationAsync("vsc-1", CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe(EDokladyErrors.Rejected.Code);
    result.Error.Type.ShouldBe(ErrorType.Failure);
  }
}
