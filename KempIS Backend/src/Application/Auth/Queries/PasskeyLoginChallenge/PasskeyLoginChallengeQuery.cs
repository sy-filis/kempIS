using Application.Abstractions.Messaging;

namespace Application.Auth.Queries.PasskeyLoginChallenge;

public sealed record PasskeyLoginChallengeQuery : IQuery<string>;
