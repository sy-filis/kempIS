using System.Text.Json.Serialization;

namespace Infrastructure.ExternalServices.Ares;

internal sealed record AresEkonomickySubjektDto(
    [property: JsonPropertyName("ico")] string Ico,
    [property: JsonPropertyName("obchodniJmeno")] string ObchodniJmeno,
    [property: JsonPropertyName("dic")] string? Dic,
    [property: JsonPropertyName("sidlo")] AresSidloDto Sidlo);

internal sealed record AresSidloDto(
    [property: JsonPropertyName("kodStatu")] string KodStatu,
    [property: JsonPropertyName("nazevObce")] string NazevObce,
    [property: JsonPropertyName("psc")] int Psc,
    [property: JsonPropertyName("nazevUlice")] string? NazevUlice,
    [property: JsonPropertyName("cisloDomovni")] int CisloDomovni);
