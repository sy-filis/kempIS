namespace Application.Abstractions.Documents;

public sealed record PdfPageOptions(
  string? Format = "A4",
  string? Width = null,
  string? Height = null,
  bool Landscape = false,
  string MarginTop = "16mm",
  string MarginBottom = "16mm",
  string MarginLeft = "12mm",
  string MarginRight = "12mm",
  bool PrintBackground = true)
{
  public static PdfPageOptions A4Portrait { get; } = new();

  public static PdfPageOptions A4Landscape { get; } = new() { Landscape = true };
}
