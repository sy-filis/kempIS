using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Finance.Invoices;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.Invoices.MarkInvoiceCreated;

internal sealed class MarkInvoiceCreatedCommandHandler(IApplicationDbContext context)
  : ICommandHandler<MarkInvoiceCreatedCommand>
{
  public async Task<Result> Handle(MarkInvoiceCreatedCommand command, CancellationToken cancellationToken)
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

    bool numberTaken = await context.Invoices
      .AnyAsync(i => i.Id != command.InvoiceId && i.Number == command.Number, cancellationToken);

    if (numberTaken)
    {
      return Result.Failure(InvoiceErrors.NumberAlreadyUsed(command.Number));
    }

    invoice.Number = command.Number;
    invoice.IssuedAt = command.IssuedAt;
    invoice.DueTo = command.DueTo;
    invoice.Status = InvoiceStatus.Created;

    invoice.Raise(new InvoiceMarkedCreatedDomainEvent(invoice.Id, command.Number));

    await context.SaveChangesAsync(cancellationToken);
    return Result.Success();
  }
}
