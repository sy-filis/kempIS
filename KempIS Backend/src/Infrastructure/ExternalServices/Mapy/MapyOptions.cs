namespace Infrastructure.ExternalServices.Mapy;

internal sealed class MapyOptions
{
  public const string SectionName = "Mapy";

  public string BaseUrl { get; set; } = string.Empty;
  public string ApiKey { get; set; } = string.Empty;
}
