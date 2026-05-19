using Application.Abstractions.Authentication;
using Application.Auth.Commands.PasskeyLoginVerify;
using SharedKernel;

namespace Application.UnitTests.Auth.Commands.PasskeyLoginVerify;

public sealed class PasskeyLoginVerifyCommandHandlerTests
{
  private readonly IPasskeyAuthenticator _authenticator = Substitute.For<IPasskeyAuthenticator>();

  private PasskeyLoginVerifyCommandHandler CreateSut() => new(_authenticator);

  [Fact]
  public async Task Handle_VerificationSucceeds_ReturnsUserId()
  {
    var userId = Guid.NewGuid();
    var outcome = new PasskeyAssertionOutcome(userId);
    _authenticator.VerifyAssertionAsync(default!, default)
        .ReturnsForAnyArgs(Result.Success(outcome));

    Result<Guid> result = await CreateSut().Handle(
        new PasskeyLoginVerifyCommand("credential-json"), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBe(userId);
  }

  [Fact]
  public async Task Handle_VerificationFails_ReturnsError()
  {
    var authError = Error.Problem("Auth.AssertionInvalid", "bad signature");
    _authenticator.VerifyAssertionAsync(default!, default)
        .ReturnsForAnyArgs(Result.Failure<PasskeyAssertionOutcome>(authError));

    Result<Guid> result = await CreateSut().Handle(
        new PasskeyLoginVerifyCommand("credential-json"), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(authError);
  }

  [Fact]
  public async Task Handle_CancellationTokenForwardedToAuthenticator()
  {
    using var cts = new CancellationTokenSource();
    _authenticator.VerifyAssertionAsync(default!, default)
        .ReturnsForAnyArgs(Result.Success(new PasskeyAssertionOutcome(Guid.NewGuid())));

    await CreateSut().Handle(new PasskeyLoginVerifyCommand("cred"), cts.Token);

    await _authenticator.Received(1).VerifyAssertionAsync("cred", cts.Token);
  }
}
