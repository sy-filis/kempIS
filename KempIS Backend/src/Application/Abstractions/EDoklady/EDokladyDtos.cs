namespace Application.Abstractions.EDoklady;

public sealed record Geolocation(double Latitude, double Longitude, int ToleranceInMeters);

public sealed record QrCode(string Data, DateTime ValidTo);

public sealed record VirtualServiceCounter(
  string Id,
  string? Name,
  QrCode QrCode,
  Geolocation? Geolocation);

public sealed record CreateVirtualServiceCounterRequest(string? Name);

public enum TransactionStateKind
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

public sealed record TransactionState(
  string Id,
  TransactionStateKind State,
  DateTime ValidTo);

public enum PresentationOutcome
{
  Success,
  Untrusted,
  UnknownError,
  MissingData,
  Expired,
}

public enum AttributeDataType
{
#pragma warning disable CA1720 // Identifier contains type name
  String,
#pragma warning restore CA1720 // Identifier contains type name
  Photo,
  Date,
  Boolean,
  Sex,
  ChangeOfData,
  Image,
}

public sealed record PresentedAttribute(
  string Name,
  AttributeDataType DataType,
  string Value);

public sealed record MissingAttribute(
  string Name,
  AttributeDataType DataType);

public sealed record PresentedDocument(
  string DocumentName,
  IReadOnlyList<PresentedAttribute> Obtained,
  IReadOnlyList<MissingAttribute>? Missing,
  string? MDoc);

public sealed record TransactionResult(
  PresentationOutcome Outcome,
  IReadOnlyList<PresentedDocument> Documents);
