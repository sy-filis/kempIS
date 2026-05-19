using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Finance.Invoices.Shared;
using Domain.Finance.InvoiceItems;
using Domain.Finance.Invoices;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.Invoices.UpdateInvoice;

internal sealed class UpdateInvoiceCommandHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateInvoiceCommand>
{
  public async Task<Result> Handle(UpdateInvoiceCommand command, CancellationToken cancellationToken)
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

    invoice.Email = command.Email;
    invoice.PhoneNumber = command.PhoneNumber;
    invoice.DueTo = command.DueTo;
    invoice.Payer = command.Payer is { } p
      ? new Payer { Name = p.Name, Surname = p.Surname, Address = p.Address }
      : null;
    invoice.LegalEntity = command.LegalEntity is { } l
      ? new LegalEntity { Name = l.Name, Cin = l.Cin, Tin = l.Tin, Address = l.Address }
      : null;

    List<InvoiceItem> existingItems = await context.InvoiceItems
      .Where(i => i.InvoiceId == invoice.Id)
      .ToListAsync(cancellationToken);

    context.InvoiceItems.RemoveRange(existingItems);

    foreach (InvoiceItemInput input in command.Items)
    {
      context.InvoiceItems.Add(new InvoiceItem
      {
        Id = Guid.NewGuid(),
        InvoiceId = invoice.Id,
        ServiceGuid = input.ServiceGuid,
        Quantity = input.Quantity,
        UnitPrice = input.UnitPrice,
        VatRatePercentage = input.VatRatePercentage,
      });
    }

    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}
