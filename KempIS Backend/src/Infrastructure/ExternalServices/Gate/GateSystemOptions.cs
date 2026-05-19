namespace Infrastructure.ExternalServices.Gate;

public sealed class GateSystemOptions
{
  public const string SectionName = "GateSystem";

  public string? BaseUrl { get; set; }

  public int TimeoutSeconds { get; set; } = 5;
}
