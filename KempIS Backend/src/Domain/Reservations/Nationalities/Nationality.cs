using SharedKernel;

namespace Domain.Reservations.Nationalities;

public sealed class Nationality : Entity
{
  public Guid Id { get; set; }
  public required string Name { get; set; }
  public required string NameEn { get; set; }
  public required string Alpha2 { get; set; }
  public required string Alpha3 { get; set; }
  public required string Numeric { get; set; }
  public required bool VisaRequired { get; set; }
  public required bool BiometricsRequired { get; set; }
  public required bool IsEu { get; set; }
  public required Guid LanguageId { get; set; }
}
