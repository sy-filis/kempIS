using Application.Abstractions.Messaging;

namespace Application.Finance.LegalEntities.Queries.GetLegalEntityFromAres;

public sealed record GetLegalEntityFromAresQuery(string Cin) : IQuery<LegalEntityFinderResponse>;
