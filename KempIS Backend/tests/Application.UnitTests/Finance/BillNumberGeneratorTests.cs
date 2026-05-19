using Infrastructure.Finance;

namespace Application.UnitTests.Finance;

public sealed class BillNumberGeneratorTests : HandlerTestBase
{
  private BillNumberGenerator CreateSut() => new(Db);

  [Fact]
  public async Task NextAsync_StartsFromOne_WhenYearUnseen()
  {
    string number = await CreateSut().NextAsync(2026, CancellationToken.None);
    number.ShouldBe("2026/0001");
  }

  [Fact]
  public async Task NextAsync_IncrementsAcrossCalls_WithinSameYear()
  {
    BillNumberGenerator sut = CreateSut();

    string first = await sut.NextAsync(2026, CancellationToken.None);
    string second = await sut.NextAsync(2026, CancellationToken.None);
    string third = await sut.NextAsync(2026, CancellationToken.None);

    first.ShouldBe("2026/0001");
    second.ShouldBe("2026/0002");
    third.ShouldBe("2026/0003");
  }

  [Fact]
  public async Task NextAsync_KeepsSeparateCountersPerYear()
  {
    BillNumberGenerator sut = CreateSut();

    string a = await sut.NextAsync(2026, CancellationToken.None);
    string b = await sut.NextAsync(2027, CancellationToken.None);
    string c = await sut.NextAsync(2026, CancellationToken.None);

    a.ShouldBe("2026/0001");
    b.ShouldBe("2027/0001");
    c.ShouldBe("2026/0002");
  }
}
