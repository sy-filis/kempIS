namespace Application.Abstractions.Documents;

public interface IPdfRenderer
{
  Task<byte[]> RenderAsync(string html, CancellationToken cancellationToken);

  Task<byte[]> RenderAsync(string html, PdfPageOptions options, CancellationToken cancellationToken);
}
