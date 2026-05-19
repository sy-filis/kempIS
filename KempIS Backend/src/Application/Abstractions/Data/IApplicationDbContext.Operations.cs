using Domain.Operations.AccessCards;
using Domain.Operations.CleanInfos;
using Domain.Operations.CleaningPlans;
using Domain.Operations.Events;
using Domain.Operations.MaintenanceIssues;
using Domain.Operations.OutOfOrders;
using Domain.Operations.SpotGroupOOFItems;
using Domain.Operations.SpotOOFItems;
using Microsoft.EntityFrameworkCore;


namespace Application.Abstractions.Data;

public partial interface IApplicationDbContext
{
  DbSet<AccessCard> AccessCards { get; }
  DbSet<CleanInfo> CleanInfos { get; }
  DbSet<CleaningPlan> CleaningPlans { get; }
  DbSet<Event> Events { get; }
  DbSet<EventSpotGroupItem> EventSpotGroupItems { get; }
  DbSet<MaintenanceIssue> MaintenanceIssues { get; }
  DbSet<OutOfOrder> OutOfOrders { get; }
  DbSet<SpotGroupOofItem> SpotGroupOofItems { get; }
  DbSet<SpotOofItem> SpotOofItems { get; }
}
