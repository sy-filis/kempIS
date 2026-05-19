using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Web.Api.OpenApi;

internal sealed class BearerSecurityOperationTransformer : IOpenApiOperationTransformer
{
  public Task TransformAsync(
    OpenApiOperation operation,
    OpenApiOperationTransformerContext context,
    CancellationToken cancellationToken)
  {
    bool hasAuth = context.Description.ActionDescriptor.EndpointMetadata
      .OfType<IAuthorizeData>()
      .Any();

    if (!hasAuth)
    {
      return Task.CompletedTask;
    }

    operation.Security ??= [];
    operation.Security.Add(new OpenApiSecurityRequirement
    {
      [new OpenApiSecuritySchemeReference("Bearer", hostDocument: null, externalResource: null)] = []
    });

    return Task.CompletedTask;
  }
}
