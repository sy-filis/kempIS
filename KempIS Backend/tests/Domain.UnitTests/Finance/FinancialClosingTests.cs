using Domain.Finance.FinancialClosings;

namespace Domain.UnitTests.Finance;

public sealed class FinancialClosingTests
{
  [Fact]
  public void Close_AssignsAllFieldsFromArguments()
  {
    uint sequentialId = 42;
    var closedAt = new DateTime(2026, 4, 24, 18, 0, 0, DateTimeKind.Utc);
    decimal total = 1234.56m;
    var createdBy = Guid.NewGuid();

    var closing = FinancialClosing.Close(sequentialId, closedAt, total, createdBy);

    closing.FinancialClosingId.ShouldBe(sequentialId);
    closing.ClosedAtUtc.ShouldBe(closedAt);
    closing.TotalAmount.ShouldBe(total);
    closing.CreatedByUserId.ShouldBe(createdBy);
  }

  [Fact]
  public void Close_GeneratesNewGuidForEachInvocation()
  {
    var a = FinancialClosing.Close(1, DateTime.UtcNow, 10m, Guid.NewGuid());
    var b = FinancialClosing.Close(2, DateTime.UtcNow, 20m, Guid.NewGuid());

    a.Id.ShouldNotBe(b.Id);
    a.Id.ShouldNotBe(Guid.Empty);
  }

  [Fact]
  public void Close_LeavesDocumentUnrenderedByDefault()
  {
    var closing = FinancialClosing.Close(1, DateTime.UtcNow, 100m, Guid.NewGuid());

    closing.DocumentContent.ShouldBeNull();
    closing.DocumentGeneratedAtUtc.ShouldBeNull();
  }
}
