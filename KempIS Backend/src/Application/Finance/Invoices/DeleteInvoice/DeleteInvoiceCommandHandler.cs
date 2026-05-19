using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Finance.Invoices;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.Invoices.DeleteInvoice;

internal sealed class DeleteInvoiceCommandHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteInvoiceCommand>
{
  public async Task<Result> Handle(DeleteInvoiceCommand command, CancellationToken cancellationToken)
  {
    Invoice? invoice = await context.Invoices
      .FirstOrDefaultAsync(i => i.Id == command.InvoiceId, cancellationToken);

    if (invoice is null)
    {
      return Result.Failure(InvoiceErrors.NotFound(command.InvoiceId));
    }

    if (invoice.Status != InvoiceStatus.Draft)
    {
      return Result.Failure(InvoiceErrors.NotDraft);
    }

    List<Domain.Finance.InvoiceItems.InvoiceItem> items = await context.InvoiceItems
      .Where(i => i.InvoiceId == invoice.Id)
      .ToListAsync(cancellationToken);

    context.InvoiceItems.RemoveRange(items);
    context.Invoices.Remove(invoice);

    await context.SaveChangesAsync(cancellationToken);
    return Result.Success();
  }
}
