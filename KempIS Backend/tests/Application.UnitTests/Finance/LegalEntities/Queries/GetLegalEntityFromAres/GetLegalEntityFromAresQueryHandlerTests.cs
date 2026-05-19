using Application.Abstractions.Finance;
using Application.Finance.LegalEntities.Queries.GetLegalEntityFromAres;
using SharedKernel;

namespace Application.UnitTests.Finance.LegalEntities.Queries.GetLegalEntityFromAres;

public sealed class GetLegalEntityFromAresQueryHandlerTests
{
  private readonly ILegalEntityFinder _finder = Substitute.For<ILegalEntityFinder>();
  private readonly GetLegalEntityFromAresQueryHandler _handler;

  public GetLegalEntityFromAresQueryHandlerTests()
  {
    _handler = new GetLegalEntityFromAresQueryHandler(_finder);
  }

  [Fact]
  public async Task Handle_With8DigitCin_DelegatesToFinder()
  {
    LegalEntityFinderResponse expected = new(
      "OLŠOVEC s.r.o.",
      "60709448",
      "CZ60709448",
      new AresAddressResponse("CZ", "Jedovnice", "67906", "Havlíčkovo náměstí", "71"));

    _finder.FindByCinAsync("60709448", Arg.Any<CancellationToken>())
      .Returns(Result.Success(expected));

    Result<LegalEntityFinderResponse> result =
      await _handler.Handle(new GetLegalEntityFromAresQuery("60709448"), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBe(expected);

    await _finder.Received(1).FindByCinAsync("60709448", Arg.Any<CancellationToken>());
  }

  [Theory]
  [InlineData("")]
  [InlineData("1234567")]
  [InlineData("123456789")]
  [InlineData("6070944A")]
  public async Task Handle_WithInvalidCin_ReturnsValidationError(string invalidCin)
  {
    Result<LegalEntityFinderResponse> result =
      await _handler.Handle(new GetLegalEntityFromAresQuery(invalidCin), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Type.ShouldBe(ErrorType.Validation);

    await _finder.DidNotReceiveWithAnyArgs().FindByCinAsync(default!, default);
  }
}
