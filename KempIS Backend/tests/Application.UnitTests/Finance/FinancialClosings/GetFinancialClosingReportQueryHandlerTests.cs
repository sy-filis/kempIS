using Application.Finance.FinancialClosings;
using Application.Finance.FinancialClosings.GetFinancialClosingReport;
using Domain.Finance.FinancialClosings;
using SharedKernel;

namespace Application.UnitTests.Finance.FinancialClosings;

public sealed class GetFinancialClosingReportQueryHandlerTests : HandlerTestBase
{
  private readonly IFinancialClosingReportRenderer _renderer =
    Substitute.For<IFinancialClosingReportRenderer>();

  private GetFinancialClosingReportQueryHandler CreateSut() => new(Db, _renderer, Clock);

  [Fact]
  public async Task Handle_ReturnsStoredDocument_WhenAlreadyRendered()
  {
    var id = Guid.NewGuid();
    Db.FinancialClosings.Add(new FinancialClosing
    {
      Id = id,
      FinancialClosingId = 42,
      ClosedAtUtc = DateTime.UtcNow,
      TotalAmount = 100m,
      DocumentContent = [1, 2, 3],
      DocumentGeneratedAtUtc = DateTime.UtcNow,
    });
    await Db.SaveChangesAsync();

    Result<GetFinancialClosingReportResponse> result =
      await CreateSut().Handle(new GetFinancialClosingReportQuery(id), default);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Content.ShouldBe([1, 2, 3]);
    result.Value.FileName.ShouldBe("financial-closing-42.pdf");
    result.Value.ContentType.ShouldBe("application/pdf");
    await _renderer.DidNotReceiveWithAnyArgs().RenderAsync(default!, default);
  }

  [Fact]
  public async Task Handle_RendersAndPersists_OnFirstCall()
  {
    var id = Guid.NewGuid();
    Db.FinancialClosings.Add(new FinancialClosing
    {
      Id = id,
      FinancialClosingId = 7,
      ClosedAtUtc = DateTime.UtcNow,
      TotalAmount = 0m,
    });
    await Db.SaveChangesAsync();

    _renderer.RenderAsync(Arg.Any<FinancialClosing>(), Arg.Any<CancellationToken>())
      .Returns(Result.Success<byte[]>([0xAA, 0xBB]));

    Result<GetFinancialClosingReportResponse> result =
      await CreateSut().Handle(new GetFinancialClosingReportQuery(id), default);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Content.ShouldBe([0xAA, 0xBB]);
    result.Value.FileName.ShouldBe("financial-closing-7.pdf");

    FinancialClosing persisted = await Db.FinancialClosings.AsNoTracking().SingleAsync();
    persisted.DocumentContent.ShouldBe([0xAA, 0xBB]);
    persisted.DocumentGeneratedAtUtc.ShouldNotBeNull();
  }

  [Fact]
  public async Task Handle_ReturnsNotFound_WhenClosingMissing()
  {
    Result<GetFinancialClosingReportResponse> result =
      await CreateSut().Handle(new GetFinancialClosingReportQuery(Guid.NewGuid()), default);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("FinancialClosings.NotFound");
    await _renderer.DidNotReceiveWithAnyArgs().RenderAsync(default!, default);
  }

  [Fact]
  public async Task Handle_ReturnsRendererFailure_WhenRendererFails()
  {
    var id = Guid.NewGuid();
    Db.FinancialClosings.Add(new FinancialClosing
    {
      Id = id,
      FinancialClosingId = 1,
      ClosedAtUtc = DateTime.UtcNow,
      TotalAmount = 0m,
    });
    await Db.SaveChangesAsync();

    _renderer.RenderAsync(Arg.Any<FinancialClosing>(), Arg.Any<CancellationToken>())
      .Returns(Result.Failure<byte[]>(Error.Problem("Render.Failed", "boom")));

    Result<GetFinancialClosingReportResponse> result =
      await CreateSut().Handle(new GetFinancialClosingReportQuery(id), default);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Render.Failed");
  }
}
