using Domain.Common;
using Domain.Operations.OutOfOrders;
using Domain.Operations.SpotGroupOOFItems;
using Domain.Operations.SpotOOFItems;
using TestUtilities.Fakes;

namespace TestUtilities.Builders;

public sealed class OutOfOrderBuilder
{
  private Guid _id = Guid.NewGuid();
  private DateOnly _from = DateOnly.FromDateTime(FakeDateTimeProvider.DefaultUtc);
  private DateOnly _to = DateOnly.FromDateTime(FakeDateTimeProvider.DefaultUtc).AddDays(7);
  private string _reason = "Maintenance";
  private readonly List<SpotOofItem> _spotItems = [];
  private readonly List<SpotGroupOofItem> _spotGroupItems = [];

  public OutOfOrderBuilder WithId(Guid id) { _id = id; return this; }
  public OutOfOrderBuilder Between(DateOnly from, DateOnly to)
  {
    _from = from;
    _to = to;
    return this;
  }
  public OutOfOrderBuilder WithReason(string reason) { _reason = reason; return this; }

  public OutOfOrder Build() => new()
  {
    Id = _id,
    Period = new DateRange(_from, _to),
    Reason = _reason,
    SpotItems = _spotItems,
    SpotGroupItems = _spotGroupItems,
  };
}
