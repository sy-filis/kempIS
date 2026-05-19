using SharedKernel;

namespace Domain.Finance.LegalEntities;

public static class LegalEntityErrors
{
  public static Error NotFound(Guid legalEntityId) => Error.NotFound(
      "LegalEntities.NotFound",
      $"The LegalEntity with the Id = '{legalEntityId}' was not found");

  public static Error NotFoundInAres(string cin) => Error.NotFound(
      "LegalEntities.NotFoundInAres",
      $"No legal entity with CIN = '{cin}' was found in ARES");

  public static readonly Error AresUnavailable = Error.Failure(
      "LegalEntities.AresUnavailable",
      "ARES is currently unavailable");
}
