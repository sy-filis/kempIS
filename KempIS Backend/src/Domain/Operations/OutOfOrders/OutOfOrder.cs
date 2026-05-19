using Domain.Common;
using Domain.Operations.SpotGroupOOFItems;
using Domain.Operations.SpotOOFItems;
using SharedKernel;

namespace Domain.Operations.OutOfOrders;

public sealed class OutOfOrder : Entity
{
  public Guid Id { get; set; }
  public required DateRange Period { get; set; }
  public required string Reason { get; set; }
  public List<SpotGroupOofItem> SpotGroupItems { get; set; } = [];
  public List<SpotOofItem> SpotItems { get; set; } = [];
}
