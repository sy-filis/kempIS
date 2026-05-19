using Application.Finance.LegalEntities.Queries.GetLegalEntityFromAres;
using SharedKernel;

namespace Application.Abstractions.Finance;

public interface ILegalEntityFinder
{
  Task<Result<LegalEntityFinderResponse>> FindByCinAsync(string cin, CancellationToken cancellationToken);
}
