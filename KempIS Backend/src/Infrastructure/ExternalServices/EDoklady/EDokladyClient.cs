using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Abstractions.EDoklady;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Infrastructure.ExternalServices.EDoklady;

internal sealed class EDokladyClient(
    HttpClient httpClient,
    IOptions<EDokladyOptions> options,
    IDateTimeProvider dateTimeProvider)
    : IEDokladyClient
{
  private const string CzechMIdDocumentName = "org.iso.18013.5.1.CZ.mID";

  private static readonly IReadOnlyList<AttributeRequestWire> RequestedAttributes =
  [
    new("portrait",                   IntentToRetain: false),
    new("given_name",                 IntentToRetain: true),
    new("family_name",                IntentToRetain: true),
    new("birth_date",                 IntentToRetain: true),
    new("nationality",                IntentToRetain: true),
    new("document_number",            IntentToRetain: true),
    new("expiry_date",                IntentToRetain: false),
    new("resident_city",              IntentToRetain: true),
    new("resident_part_of_city",      IntentToRetain: true),
    new("resident_street",            IntentToRetain: true),
    new("resident_city_house_number", IntentToRetain: true),
  ];

  public async Task<Result<VirtualServiceCounter>> CreateVirtualServiceCounterAsync(
      CreateVirtualServiceCounterRequest request,
      CancellationToken cancellationToken)
  {
    EDokladyOptions.GeolocationOptions? geo = options.Value.Geolocation;
    VirtualServiceCounterCreateWire payload = new(
        request.Name,
        geo is null
          ? null
          : new GeolocationWire(geo.Latitude, geo.Longitude, geo.ToleranceInMeters));

    Result<VirtualServiceCounterWire> sendResult = await SendAsync<VirtualServiceCounterWire>(
        HttpMethod.Post,
        "integration/virtualServiceCounters",
        payload,
        resourceId: "(new)",
        cancellationToken);

    return sendResult.IsSuccess ? Result.Success(Map(sendResult.Value)) : Result.Failure<VirtualServiceCounter>(sendResult.Error);
  }

  public async Task<Result<VirtualServiceCounter>> GetVirtualServiceCounterAsync(
      string id, CancellationToken cancellationToken)
  {
    Result<VirtualServiceCounterWire> counterResult = await SendAsync<VirtualServiceCounterWire>(
        HttpMethod.Get,
        $"integration/virtualServiceCounters/{id}",
        body: null,
        resourceId: id,
        cancellationToken);

    if (counterResult.IsFailure)
    {
      return Result.Failure<VirtualServiceCounter>(counterResult.Error);
    }

    VirtualServiceCounterWire wire = counterResult.Value;
    QrCodeWire effectiveQr = wire.QrCode;

    DateTime threshold = dateTimeProvider.UtcNow.AddDays(options.Value.QrCodeRefreshThresholdDays);
    if (wire.QrCode.ValidTo <= threshold)
    {
      Result<QrCodeWire> regenResult = await SendAsync<QrCodeWire>(
          HttpMethod.Post,
          $"integration/virtualServiceCounters/{id}/generateQrCode",
          body: null,
          resourceId: id,
          cancellationToken);

      if (regenResult.IsFailure)
      {
        return Result.Failure<VirtualServiceCounter>(regenResult.Error);
      }

      effectiveQr = regenResult.Value;
    }

    return Result.Success(new VirtualServiceCounter(
        wire.Id,
        wire.Name,
        new QrCode(effectiveQr.Data, effectiveQr.ValidTo),
        wire.Geolocation is null ? null : new Geolocation(wire.Geolocation.Latitude, wire.Geolocation.Longitude, wire.Geolocation.ToleranceInMeters)));
  }

  public async Task<Result<string>> StartPresentationAsync(
      string virtualServiceCounterId, CancellationToken cancellationToken)
  {
    RequestServerPresentationWire payload = new(
        RequestDocuments:
        [
          new DocumentRequestWire(CzechMIdDocumentName, RequestedAttributes),
        ],
        VirtualServiceCounterId: virtualServiceCounterId);

    Result<RequestServerPresentationResponseWire> sendResult = await SendAsync<RequestServerPresentationResponseWire>(
        HttpMethod.Post,
        "integration/presentation/server/requestPresentation",
        payload,
        resourceId: virtualServiceCounterId,
        cancellationToken);

    return sendResult.IsSuccess
        ? Result.Success(sendResult.Value.TransactionId)
        : Result.Failure<string>(sendResult.Error);
  }

  public async Task<Result<TransactionState>> GetTransactionAsync(
      string transactionId, CancellationToken cancellationToken)
  {
    Result<ServerFlowTransactionWire> sendResult = await SendAsync<ServerFlowTransactionWire>(
        HttpMethod.Get,
        $"integration/presentation/server/transactions/{transactionId}",
        body: null,
        resourceId: transactionId,
        cancellationToken);

    if (sendResult.IsFailure)
    {
      return Result.Failure<TransactionState>(sendResult.Error);
    }

    ServerFlowTransactionWire wire = sendResult.Value;
    return Result.Success(new TransactionState(
        wire.Id,
        (TransactionStateKind)wire.State,
        wire.ValidTo));
  }

  public async Task<Result<TransactionResult>> GetTransactionResultAsync(
      string transactionId,
      bool includeMDoc,
      bool includeMissingCredentials,
      CancellationToken cancellationToken)
  {
    string path = $"integration/presentation/server/transactions/{transactionId}/result"
                + $"?includeMDoc={(includeMDoc ? "true" : "false")}"
                + $"&includeMissingCredentials={(includeMissingCredentials ? "true" : "false")}";

    Result<TransactionResultWire> sendResult = await SendAsync<TransactionResultWire>(
        HttpMethod.Get, path, body: null, resourceId: transactionId, cancellationToken);

    if (sendResult.IsFailure)
    {
      return Result.Failure<TransactionResult>(sendResult.Error);
    }

    TransactionResultWire wire = sendResult.Value;

    IReadOnlyList<PresentedDocument> documents = wire.ObtainedDocuments is null
        ? []
        : wire.ObtainedDocuments
            .Select(d => new PresentedDocument(
                d.DocumentName,
                d.ObtainedCredentials
                    .Select(c => new PresentedAttribute(c.Name, (AttributeDataType)c.AttributeDataType, c.Value))
                    .ToArray(),
                d.MissingCredentials?
                    .Select(m => new MissingAttribute(m.Name, (AttributeDataType)m.AttributeDataType))
                    .ToArray(),
                d.MDoc))
            .ToArray();

    return Result.Success(new TransactionResult((PresentationOutcome)wire.PresentationResult, documents));
  }

  private static VirtualServiceCounter Map(VirtualServiceCounterWire w) =>
      new(w.Id,
          w.Name,
          new QrCode(w.QrCode.Data, w.QrCode.ValidTo),
          w.Geolocation is null ? null : new Geolocation(w.Geolocation.Latitude, w.Geolocation.Longitude, w.Geolocation.ToleranceInMeters));

  private async Task<Result<TResponse>> SendAsync<TResponse>(
      HttpMethod method,
      string relativePath,
      object? body,
      string resourceId,
      CancellationToken cancellationToken)
      where TResponse : class
  {
    HttpResponseMessage response;
    try
    {
      using HttpRequestMessage request = new(method, new Uri(relativePath, UriKind.Relative));
      if (body is not null)
      {
        request.Content = JsonContent.Create(body, options: EDokladyJsonOptions.Default);
      }
      response = await httpClient.SendAsync(request, cancellationToken);
    }
    catch (HttpRequestException)
    {
      return Result.Failure<TResponse>(EDokladyErrors.Unavailable);
    }
    catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
      return Result.Failure<TResponse>(EDokladyErrors.Unavailable);
    }

    try
    {
      if (response.StatusCode == HttpStatusCode.NotFound)
      {
        return Result.Failure<TResponse>(EDokladyErrors.NotFound(resourceId));
      }

      if (response.StatusCode == HttpStatusCode.BadRequest)
      {
        string detail = await ReadErrorDetailAsync(response, cancellationToken);
        return Result.Failure<TResponse>(EDokladyErrors.BadRequest(detail));
      }

      if (!response.IsSuccessStatusCode)
      {
        return Result.Failure<TResponse>(EDokladyErrors.Rejected);
      }

      TResponse? parsed;
      try
      {
        parsed = await response.Content.ReadFromJsonAsync<TResponse>(EDokladyJsonOptions.Default, cancellationToken);
      }
      catch (JsonException)
      {
        return Result.Failure<TResponse>(EDokladyErrors.Rejected);
      }

      return parsed is null
          ? Result.Failure<TResponse>(EDokladyErrors.Rejected)
          : Result.Success(parsed);
    }
    finally
    {
      response.Dispose();
    }
  }

  private static async Task<string> ReadErrorDetailAsync(HttpResponseMessage response, CancellationToken ct)
  {
    try
    {
      ErrorWire? err = await response.Content.ReadFromJsonAsync<ErrorWire>(EDokladyJsonOptions.Default, ct);
      return err?.Errors is { Count: > 0 }
          ? string.Join("; ", err.Errors)
          : "eDoklady rejected the request";
    }
    catch (JsonException)
    {
      return "eDoklady rejected the request";
    }
  }
}
