using Application.Abstractions.Data;
using Domain.Finance.Bills;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Finance.Bills;

internal sealed class BillCreatedDomainEventHandler(
  IApplicationDbContext db,
  IBillDocumentRenderer renderer,
  IDateTimeProvider dateTimeProvider,
  ILogger<BillCreatedDomainEventHandler> logger)
  : IDomainEventHandler<BillCreatedDomainEvent>
{
  public async Task Handle(BillCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
  {
    try
    {
      Bill? bill = await db.Bills
        .FirstOrDefaultAsync(b => b.Id == domainEvent.BillId, cancellationToken);

      if (bill is null)
      {
        logger.LogWarning("Cannot render PDF: bill {BillId} not found", domainEvent.BillId);
        return;
      }

      Result<BillDocumentRenderResult> rendered =
        await renderer.RenderAsync(domainEvent.BillId, cancellationToken);

      if (rendered.IsFailure)
      {
        logger.LogWarning(
          "Failed to render PDF for bill {BillId}: {Error}",
          domainEvent.BillId,
          rendered.Error);
        return;
      }

      bill.DocumentContent = rendered.Value.Content;
      bill.DocumentGeneratedAtUtc = dateTimeProvider.UtcNow;

      await db.SaveChangesAsync(cancellationToken);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Unhandled exception while rendering PDF for bill {BillId}", domainEvent.BillId);
    }
  }
}
