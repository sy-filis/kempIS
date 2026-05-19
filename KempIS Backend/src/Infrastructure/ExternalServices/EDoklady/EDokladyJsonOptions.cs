using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infrastructure.ExternalServices.EDoklady;

internal static class EDokladyJsonOptions
{
  public static readonly JsonSerializerOptions Default = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) },
  };
}
