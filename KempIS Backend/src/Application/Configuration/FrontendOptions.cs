using System.ComponentModel.DataAnnotations;

namespace Application.Configuration;

public sealed class FrontendOptions
{
  public const string SectionName = "Frontend";

  [Required]
  [Url]
  public string BaseUrl { get; set; } = string.Empty;
}
