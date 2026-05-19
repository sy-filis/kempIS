using System.Text.Json.Serialization;

namespace Infrastructure.ExternalServices.Ruian;

internal sealed record RuianFindAddressCandidatesDto(
  [property: JsonPropertyName("candidates")] IReadOnlyList<RuianCandidate>? Candidates);

internal sealed record RuianCandidate(
  [property: JsonPropertyName("address")] string? Address,
  [property: JsonPropertyName("score")] double Score);
