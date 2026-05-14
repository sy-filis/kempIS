using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LocalPrintServerService;

public static class OpenApiResponseExamplesExtensions
{
  public static TBuilder WithResponseExample<TBuilder>(
      this TBuilder builder,
      int statusCode,
      object? example,
      string mediaType = "application/json")
      where TBuilder : IEndpointConventionBuilder
  {
    JsonNode? node = example is null ? null : JsonSerializer.SerializeToNode(example);
    return builder.WithMetadata(new ResponseExampleMetadata(statusCode, mediaType, node));
  }
}

internal sealed record ResponseExampleMetadata(int StatusCode, string MediaType, JsonNode? Example);

public sealed class ResponseExamplesTransformer : IOpenApiOperationTransformer
{
  public Task TransformAsync(
      OpenApiOperation operation,
      OpenApiOperationTransformerContext context,
      CancellationToken cancellationToken)
  {
    OpenApiResponses? responses = operation.Responses;
    if (responses is null)
    {
      return Task.CompletedTask;
    }

    IEnumerable<ResponseExampleMetadata> examples = context.Description.ActionDescriptor.EndpointMetadata
        .OfType<ResponseExampleMetadata>();

    foreach (ResponseExampleMetadata example in examples)
    {
      string key = example.StatusCode.ToString(CultureInfo.InvariantCulture);
      if (!responses.TryGetValue(key, out IOpenApiResponse? response))
      {
        continue;
      }

      if (response.Content is not { } content)
      {
        continue;
      }

      if (!content.TryGetValue(example.MediaType, out OpenApiMediaType? media) || media is null)
      {
        continue;
      }

      media.Example = example.Example;
    }

    return Task.CompletedTask;
  }
}
