using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Finance.Invoices;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.Invoices.MarkInvoicePaid;

internal sealed class MarkInvoicePaidCommandHandler(IApplicationDbContext context)
  : ICommandHandler<MarkInvoicePaidCommand>
{
  public async Task<Result> Handle(MarkInvoicePaidCommand command, CancellationToken cancellationToken)
  {
    Invoice? invoice = await context.Invoices
      .FirstOrDefaultAsync(i => i.Id == command.InvoiceId, cancellationToken);

    if (invoice is null)
    {
      return Result.Failure(InvoiceErrors.NotFound(command.InvoiceId));
    }

    if (invoice.Status != InvoiceStatus.Created)
    {
      return Result.Failure(InvoiceErrors.NotCreated);
    }

    invoice.Status = InvoiceStatus.Paid;
    invoice.PaidAt = command.PaidAt;

    invoice.Raise(new InvoiceMarkedPaidDomainEvent(invoice.Id));

    await context.SaveChangesAsync(cancellationToken);
    return Result.Success();
  }
}
