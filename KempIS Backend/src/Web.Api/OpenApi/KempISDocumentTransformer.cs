using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Web.Api.Endpoints;

namespace Web.Api.OpenApi;

[SuppressMessage(
  "Major Code Smell",
  "S1075:URIs should not be hardcoded",
  Justification = "OpenAPI document metadata.")]
internal sealed class KempISDocumentTransformer : IOpenApiDocumentTransformer
{
  public Task TransformAsync(
    OpenApiDocument document,
    OpenApiDocumentTransformerContext context,
    CancellationToken cancellationToken)
  {
    document.Info = new OpenApiInfo
    {
      Title = "KempIS Backend API",
      Version = "1.0.0",
      Contact = new OpenApiContact
      {
        Name = "Filip Zukal",
        Email = "filip@zukalovi.eu"
      },
      License = new OpenApiLicense
      {
        Name = "MIT",
        Url = new Uri("https://opensource.org/licenses/MIT")
      }
    };

    document.Servers =
    [
      new OpenApiServer
      {
        Url = "http://localhost:5000",
        Description = "Local development"
      }
    ];

    document.Tags = new HashSet<OpenApiTag>(
      Tags.All.Select(t => new OpenApiTag { Name = t.Name, Description = t.Description }));

    return Task.CompletedTask;
  }
}
