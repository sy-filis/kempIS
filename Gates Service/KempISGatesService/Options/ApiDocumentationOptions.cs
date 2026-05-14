namespace KempISGatesService.Options;

public sealed class ApiDocumentationOptions
{
  public const string SectionName = "ApiDocumentation";

  // When true, /openapi/v1.json and /scalar/v1 are mapped. Disabled in production deployments.
  public bool Enabled { get; init; }
}
