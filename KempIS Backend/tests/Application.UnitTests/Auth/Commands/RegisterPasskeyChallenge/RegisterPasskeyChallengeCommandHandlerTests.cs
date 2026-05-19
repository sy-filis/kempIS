using Application.Abstractions.Authentication;
using Application.Auth.Commands.RegisterPasskeyChallenge;
using SharedKernel;

namespace Application.UnitTests.Auth.Commands.RegisterPasskeyChallenge;

public sealed class RegisterPasskeyChallengeCommandHandlerTests
{
  private readonly StubIdentityService _identity = new();
  private readonly IPasskeyAuthenticator _authenticator = Substitute.For<IPasskeyAuthenticator>();

  private RegisterPasskeyChallengeCommandHandler CreateSut() => new(_identity, _authenticator);

  private static UserDetail UserDetail(Guid id, string username = "alice", string name = "Alice Walker") =>
    new(id, username, name, ["Manager"], false, DateTime.MinValue, 0);

  [Fact]
  public async Task Handle_UserExists_ReturnsRegistrationOptionsFromAuthenticator()
  {
    var userId = Guid.NewGuid();
    _identity.GetUserAsyncResult = Result.Success(UserDetail(userId, "alice", "Alice Walker"));
    _authenticator.CreateRegistrationOptionsAsync(Guid.Empty, default!, default!, default)
        .ReturnsForAnyArgs(Result.Success("{\"options\":\"payload\"}"));

    Result<string> result = await CreateSut().Handle(
        new RegisterPasskeyChallengeCommand(userId),
        CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBe("{\"options\":\"payload\"}");
    await _authenticator.Received(1).CreateRegistrationOptionsAsync(
        userId, "alice", "Alice Walker", Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_UserNotFound_ReturnsLookupError_AuthenticatorNotInvoked()
  {
    var userId = Guid.NewGuid();
    _identity.GetUserAsyncResult = Result.Failure<UserDetail>(IdentityErrors.UserNotFound(userId));

    Result<string> result = await CreateSut().Handle(
        new RegisterPasskeyChallengeCommand(userId),
        CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Identity.UserNotFound");
    await _authenticator.DidNotReceiveWithAnyArgs().CreateRegistrationOptionsAsync(Guid.Empty, default!, default!, default);
  }

  [Fact]
  public async Task Handle_AuthenticatorFailsAfterUserLookup_PropagatesAuthenticatorError()
  {
    var userId = Guid.NewGuid();
    _identity.GetUserAsyncResult = Result.Success(UserDetail(userId, "u"));
    var authError = Error.Problem("Passkey.OptionsFailed", "fido2 offline");
    _authenticator.CreateRegistrationOptionsAsync(Guid.Empty, default!, default!, default)
        .ReturnsForAnyArgs(Result.Failure<string>(authError));

    Result<string> result = await CreateSut().Handle(
        new RegisterPasskeyChallengeCommand(userId),
        CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(authError);
  }
}
