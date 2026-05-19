using Application.Abstractions.Authentication;
using Application.Auth.Commands.RegisterPasskeyVerify;
using SharedKernel;

namespace Application.UnitTests.Auth.Commands.RegisterPasskeyVerify;

public sealed class RegisterPasskeyVerifyCommandHandlerTests
{
  private readonly IPasskeyAuthenticator _authenticator = Substitute.For<IPasskeyAuthenticator>();

  private RegisterPasskeyVerifyCommandHandler CreateSut() => new(_authenticator);

  [Fact]
  public async Task Handle_AttestationValid_ReturnsSuccess()
  {
    _authenticator.VerifyAttestationAsync(default!, default!, default)
        .ReturnsForAnyArgs(Result.Success(new PasskeyAttestationOutcome(Guid.NewGuid())));

    Result result = await CreateSut().Handle(
        new RegisterPasskeyVerifyCommand("cred"), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
  }

  [Fact]
  public async Task Handle_AttestationFails_ReturnsPropagatedError()
  {
    var error = Error.Problem("Passkey.AttestationInvalid", "bad attestation");
    _authenticator.VerifyAttestationAsync(default!, default!, default)
        .ReturnsForAnyArgs(Result.Failure<PasskeyAttestationOutcome>(error));

    Result result = await CreateSut().Handle(
        new RegisterPasskeyVerifyCommand("cred"), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(error);
  }
}
