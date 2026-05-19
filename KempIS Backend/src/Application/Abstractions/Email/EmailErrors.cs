using SharedKernel;

namespace Application.Abstractions.Email;

public static class EmailErrors
{
  public static Error TemplateNotFound(string templateName, string language) => Error.NotFound(
      "Email.TemplateNotFound",
      $"Email template '{templateName}' for language '{language}' was not found.");

  public static Error TemplateMalformed(string templateName, string language) => Error.Problem(
      "Email.TemplateMalformed",
      $"Email template '{templateName}' for language '{language}' is malformed (expected a subject, a '---' separator, and a body).");
}
