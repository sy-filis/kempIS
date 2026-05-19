namespace Infrastructure.Email;

public sealed class EmailTemplateOptions
{
  public const string SectionName = "EmailTemplates";

  public string Root { get; set; } = "EmailTemplates";

  public string DefaultLanguage { get; set; } = "en";
}
