using Application.Abstractions.Messaging;

namespace Application.Reception.PairCodes.Commands.CreatePairCode;

public sealed record CreatePairCodeCommand : ICommand<CreatePairCodeResponse>;
