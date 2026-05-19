using SharedKernel;

namespace Application.Abstractions.Email;

public interface IEmailTemplateRenderer
{
  Task<Result<RenderedEmail>> RenderAsync(
    string templateName,
    string language,
    IReadOnlyDictionary<string, string> values,
    CancellationToken cancellationToken);
}

public sealed record RenderedEmail(string Subject, string Body);
