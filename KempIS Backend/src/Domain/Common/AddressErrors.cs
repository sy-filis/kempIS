using SharedKernel;

namespace Domain.Common;

public static class AddressErrors
{
  public static readonly Error QueryTooShort = Error.Problem(
    "Addresses.QueryTooShort",
    "Query must be longer than 8 characters.");

  public static readonly Error LimitOutOfRange = Error.Problem(
    "Addresses.LimitOutOfRange",
    "Limit must be greater than or equal to 1.");

  public static readonly Error ProviderUnavailable = Error.Failure(
    "Addresses.ProviderUnavailable",
    "Address suggestion providers are currently unavailable.");
}
