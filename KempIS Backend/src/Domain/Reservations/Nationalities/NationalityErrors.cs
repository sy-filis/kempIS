using SharedKernel;

namespace Domain.Reservations.Nationalities;

public static class NationalityErrors
{
  public static Error NotFound(Guid nationalityId) => Error.NotFound(
      "Nationality.NotFound",
      $"The Nationality with the Id = '{nationalityId}' was not found");

  public static Error HasReferences(Guid nationalityId) => Error.Conflict(
      "Nationality.HasReferences",
      $"The Nationality with the Id = '{nationalityId}' cannot be deleted because it is referenced by one or more guests");

  public static Error Alpha2Exists(string alpha2) => Error.Conflict(
      "Nationalities.Alpha2Exists",
      $"A nationality with Alpha2 code '{alpha2}' already exists.");

  public static Error Alpha3Exists(string alpha3) => Error.Conflict(
      "Nationalities.Alpha3Exists",
      $"A nationality with Alpha3 code '{alpha3}' already exists.");

  public static Error NumericExists(string numeric) => Error.Conflict(
      "Nationalities.NumericExists",
      $"A nationality with numeric code '{numeric}' already exists.");
}
