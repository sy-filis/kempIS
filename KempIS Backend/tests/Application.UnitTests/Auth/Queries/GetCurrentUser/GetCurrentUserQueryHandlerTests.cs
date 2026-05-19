using Application.Abstractions.Authentication;
using Application.Auth.Queries.GetCurrentUser;
using SharedKernel;

namespace Application.UnitTests.Auth.Queries.GetCurrentUser;

public sealed class GetCurrentUserQueryHandlerTests
{
  private readonly IUserContext _userContext = Substitute.For<IUserContext>();
  private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
  private readonly INoAuthState _noAuth = Substitute.For<INoAuthState>();

  private GetCurrentUserQueryHandler CreateSut() => new(_userContext, _identity, _noAuth);

  [Fact]
  public async Task Handle_NoAuthDisabled_LooksUpUserViaIdentityService()
  {
    var userId = Guid.NewGuid();
    DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
    var detail = new UserDetail(userId, "alice", "Alice Doe", ["Manager"], false, DateTime.UtcNow, 0);

    _noAuth.IsEnabled.Returns(false);
    _userContext.UserId.Returns(userId);
    _userContext.SessionExpiresAt.Returns(expiresAt);
    _identity.GetUserAsync(userId, Arg.Any<CancellationToken>())
      .Returns(Result.Success(detail));

    Result<CurrentUserResponse> result =
      await CreateSut().Handle(new GetCurrentUserQuery(), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Id.ShouldBe(userId);
    result.Value.Username.ShouldBe("alice");
    result.Value.Name.ShouldBe("Alice Doe");
    result.Value.Roles.ShouldBe(["Manager"]);
    result.Value.SessionExpiresAt.ShouldBe(expiresAt);
  }

  [Fact]
  public async Task Handle_NoAuthEnabled_ReturnsSyntheticResponseWithoutIdentityServiceCall()
  {
    DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddHours(1);

    _noAuth.IsEnabled.Returns(true);
    _userContext.UserId.Returns(NoAuthHandlerConstants.DevUserId);
    _userContext.SessionExpiresAt.Returns(expiresAt);

    Result<CurrentUserResponse> result =
      await CreateSut().Handle(new GetCurrentUserQuery(), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Id.ShouldBe(NoAuthHandlerConstants.DevUserId);
    result.Value.Username.ShouldBe("no-auth");
    result.Value.Name.ShouldBe("Setup Operator");
    result.Value.Roles.ShouldBe(Roles.All);
    result.Value.SessionExpiresAt.ShouldBe(expiresAt);

    await _identity.DidNotReceiveWithAnyArgs().GetUserAsync(Guid.Empty, CancellationToken.None);
  }

  // Mirror of Web.Api.NoAuthHandler.DevUserId; this test project cannot reference Web.Api.
  private static class NoAuthHandlerConstants
  {
    public static readonly Guid DevUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
  }
}
