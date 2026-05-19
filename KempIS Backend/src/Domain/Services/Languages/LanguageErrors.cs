using SharedKernel;

namespace Domain.Services;

public static class LanguageErrors
{
  public static Error NotFound(Guid languageId) => Error.NotFound(
      "Language.NotFound",
      $"The Language with the Id = '{languageId}' was not found");

  public static Error HasReferences(Guid languageId) => Error.Conflict(
      "Language.HasReferences",
      $"The Language with the Id = '{languageId}' cannot be deleted because it is referenced by one or more nationalities");
}
