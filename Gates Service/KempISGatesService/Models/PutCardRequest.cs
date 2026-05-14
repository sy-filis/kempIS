namespace KempISGatesService.Models;

public sealed record PutCardRequest(DateTimeOffset ValidTo, string RealName = "", string Note = "")
{
  // Mirrors the legacy Card.Name / Card.Notes column widths.
  public const int MaxFieldLength = 64;
}
