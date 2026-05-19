using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Configuration;
using Application.Finance.Invoices.Shared;
using Domain.Finance.InvoiceItems;
using Domain.Finance.Invoices;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Application.Finance.Invoices.CreateInvoice;

internal sealed class CreateInvoiceCommandHandler(
  IApplicationDbContext context,
  IDateTimeProvider dateTimeProvider,
  IOptions<RetentionSettings> retentionSettings)
  : ICommandHandler<CreateInvoiceCommand, CreateInvoiceResponse>
{
  public async Task<Result<CreateInvoiceResponse>> Handle(
    CreateInvoiceCommand command,
    CancellationToken cancellationToken)
  {
    DateTime now = dateTimeProvider.UtcNow;
    var today = DateOnly.FromDateTime(now);

    var invoice = new Invoice
    {
      Id = Guid.NewGuid(),
      ReservationId = command.ReservationId,
      Status = InvoiceStatus.Draft,
      Number = null,
      IssuedAt = today,
      DueTo = command.DueTo,
      Email = command.Email,
      PhoneNumber = command.PhoneNumber,
      Payer = command.Payer is { } p
        ? new Payer { Name = p.Name, Surname = p.Surname, Address = p.Address }
        : null,
      LegalEntity = command.LegalEntity is { } l
        ? new LegalEntity { Name = l.Name, Cin = l.Cin, Tin = l.Tin, Address = l.Address }
        : null,
      Scartation = today.AddYears(retentionSettings.Value.InvoiceYears),
    };

    context.Invoices.Add(invoice);

    foreach (InvoiceItemInput item in command.Items)
    {
      context.InvoiceItems.Add(new InvoiceItem
      {
        Id = Guid.NewGuid(),
        InvoiceId = invoice.Id,
        ServiceGuid = item.ServiceGuid,
        Quantity = item.Quantity,
        UnitPrice = item.UnitPrice,
        VatRatePercentage = item.VatRatePercentage,
      });
    }

    invoice.Raise(new InvoiceCreatedDomainEvent(invoice.Id));

    await context.SaveChangesAsync(cancellationToken);

    return Result.Success(new CreateInvoiceResponse(invoice.Id));
  }
}
