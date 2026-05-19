using System.Text.Json.Serialization;

namespace Infrastructure.ExternalServices.EDoklady;

internal sealed record GeolocationWire(
    double Latitude,
    double Longitude,
    int ToleranceInMeters);

internal sealed record QrCodeWire(
    string Data,
    DateTime ValidTo);

internal sealed record VirtualServiceCounterWire(
    string Id,
    string? Name,
    QrCodeWire QrCode,
    GeolocationWire? Geolocation);

internal sealed record VirtualServiceCounterCreateWire(
    string? Name,
    GeolocationWire? Geolocation);

internal sealed record AttributeRequestWire(
    string Name,
    bool IntentToRetain);

internal sealed record DocumentRequestWire(
    string DocumentName,
    IReadOnlyList<AttributeRequestWire> Attributes);

internal sealed record RequestServerPresentationWire(
    IReadOnlyList<DocumentRequestWire> RequestDocuments,
    string VirtualServiceCounterId);

internal sealed record RequestServerPresentationResponseWire(
    string TransactionId);

// Ordinals must stay aligned with Application.Abstractions.EDoklady.TransactionStateKind (cast by integer).
internal enum ServerFlowStateWire
{
  Canceled,
  Failed,
  Finished,
  Open,
  ResponseReceived,
  Unfinished,
  WaitingForResponse,
  Timeout,
}

internal sealed record ServerFlowTransactionWire(
    string Id,
    ServerFlowStateWire State,
    DateTime ValidTo);

// Ordinals must stay aligned with Application.Abstractions.EDoklady.PresentationOutcome (cast by integer).
internal enum PresentationResultWire
{
  Success,
  Untrusted,
  UnknownError,
  MissingData,
  Expired,
}

// Ordinals must stay aligned with Application.Abstractions.EDoklady.AttributeDataType (cast by integer).
internal enum AttributeDataTypeWire
{
#pragma warning disable CA1720
  String,
#pragma warning restore CA1720
  Photo,
  Date,
  Boolean,
  Sex,
  ChangeOfData,
  Image,
}

internal sealed record PresentedCredentialWire(
    string Name,
    AttributeDataTypeWire AttributeDataType,
    string Value);

internal sealed record MissingCredentialWire(
    string Name,
    AttributeDataTypeWire AttributeDataType);

internal sealed record DocumentResponseWire(
    string DocumentName,
    IReadOnlyList<PresentedCredentialWire> ObtainedCredentials,
    IReadOnlyList<MissingCredentialWire>? MissingCredentials,
    [property: JsonPropertyName("MDoc")] string? MDoc);

internal sealed record TransactionResultWire(
    PresentationResultWire PresentationResult,
    IReadOnlyList<DocumentResponseWire>? ObtainedDocuments);

internal sealed record ErrorWire(IReadOnlyList<string>? Errors);
