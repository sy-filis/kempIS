namespace Application.Reception.PairCodes.Commands.CreatePairCode;

public sealed record CreatePairCodeResponse(string PairCode, DateTime ExpiresAtUtc);
