using Application.Abstractions.Email;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Infrastructure.Email;

internal sealed class FileEmailTemplateRenderer(IOptions<EmailTemplateOptions> options)
  : IEmailTemplateRenderer
{
  private const string Separator = "---";

  private readonly EmailTemplateOptions _options = options.Value;

  public async Task<Result<RenderedEmail>> RenderAsync(
    string templateName,
    string language,
    IReadOnlyDictionary<string, string> values,
    CancellationToken cancellationToken)
  {
    string? content = await ReadTemplateAsync(templateName, language, cancellationToken);

    if (content is null && !string.Equals(language, _options.DefaultLanguage, StringComparison.OrdinalIgnoreCase))
    {
      content = await ReadTemplateAsync(templateName, _options.DefaultLanguage, cancellationToken);
    }

    if (content is null)
    {
      return Result.Failure<RenderedEmail>(EmailErrors.TemplateNotFound(templateName, language));
    }

    int separatorIndex = FindSeparatorIndex(content);
    if (separatorIndex < 0)
    {
      return Result.Failure<RenderedEmail>(EmailErrors.TemplateMalformed(templateName, language));
    }

    string subjectRaw = content[..separatorIndex].Trim();
    string bodyRaw = content[(separatorIndex + Separator.Length)..].TrimStart('\r', '\n');

    string subject = Substitute(subjectRaw, values);
    string body = Substitute(bodyRaw, values);

    return new RenderedEmail(subject, body);
  }

  private async Task<string?> ReadTemplateAsync(string templateName, string language, CancellationToken cancellationToken)
  {
    string path = ResolvePath(templateName, language);
    if (!File.Exists(path))
    {
      return null;
    }

    return await File.ReadAllTextAsync(path, cancellationToken);
  }

  private string ResolvePath(string templateName, string language)
  {
    string root = Path.IsPathRooted(_options.Root)
      ? _options.Root
      : Path.Combine(AppContext.BaseDirectory, _options.Root);

    return Path.Combine(root, templateName, $"{language}.txt");
  }

  private static int FindSeparatorIndex(string content)
  {
    string[] lines = content.Split('\n');
    int offset = 0;
    foreach (string line in lines)
    {
      if (line.TrimEnd('\r').Trim() == Separator)
      {
        return offset;
      }
      offset += line.Length + 1;
    }
    return -1;
  }

  private static string Substitute(string template, IReadOnlyDictionary<string, string> values)
  {
    string result = template;
    foreach (KeyValuePair<string, string> kvp in values)
    {
      result = result.Replace("{{" + kvp.Key + "}}", kvp.Value, StringComparison.Ordinal);
    }
    return result;
  }
}
