using System.Text.RegularExpressions;
using Application.Abstractions.Finance;
using Application.Abstractions.Messaging;
using SharedKernel;

namespace Application.Finance.LegalEntities.Queries.GetLegalEntityFromAres;

internal sealed partial class GetLegalEntityFromAresQueryHandler(ILegalEntityFinder finder)
  : IQueryHandler<GetLegalEntityFromAresQuery, LegalEntityFinderResponse>
{
  private static readonly Error CinFormatError = Error.Problem(
    "LegalEntity.Cin.Invalid",
    "CIN must be exactly 8 digits.");

  [GeneratedRegex("^\\d{8}$")]
  private static partial Regex CinRegex();

  public Task<Result<LegalEntityFinderResponse>> Handle(
    GetLegalEntityFromAresQuery query,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrEmpty(query.Cin) || !CinRegex().IsMatch(query.Cin))
    {
      return Task.FromResult(
        Result.Failure<LegalEntityFinderResponse>(new ValidationError([CinFormatError])));
    }

    return finder.FindByCinAsync(query.Cin, cancellationToken);
  }
}
