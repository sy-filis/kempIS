namespace Web.Api.Infrastructure;

internal sealed class CorsPolicyOptions
{
  public const string SectionName = "Cors";

  public string[] AllowedOrigins { get; set; } = [];

  public string[] AllowedMethods { get; set; } = [];

  public string[] AllowedHeaders { get; set; } = [];

  public string[] ExposedHeaders { get; set; } = [];

  public bool AllowCredentials { get; set; }
}
