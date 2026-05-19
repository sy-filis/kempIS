using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Finance.Invoices;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.Invoices.GetInvoiceById;

internal sealed class GetInvoiceByIdQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetInvoiceByIdQuery, GetInvoiceByIdResponse>
{
  public async Task<Result<GetInvoiceByIdResponse>> Handle(
    GetInvoiceByIdQuery query,
    CancellationToken cancellationToken)
  {
    GetInvoiceByIdResponse? response = await context.Invoices
      .AsNoTracking()
      .Where(i => i.Id == query.InvoiceId)
      .Select(i => new GetInvoiceByIdResponse(
        i.Id,
        i.ReservationId,
        i.Number,
        i.Status,
        i.IssuedAt,
        i.PaidAt,
        i.DueTo,
        i.LinkedBillId,
        i.Email,
        i.PhoneNumber,
        i.Payer == null
          ? null
          : new InvoicePayerView(i.Payer.Name, i.Payer.Surname, i.Payer.Address),
        i.LegalEntity == null
          ? null
          : new InvoiceLegalEntityView(i.LegalEntity.Name, i.LegalEntity.Cin, i.LegalEntity.Tin, i.LegalEntity.Address),
        context.InvoiceItems
          .Where(item => item.InvoiceId == i.Id)
          .Select(item => new InvoiceItemView(
            item.Id, item.ServiceGuid, item.Quantity, item.UnitPrice, item.VatRatePercentage))
          .ToList()))
      .FirstOrDefaultAsync(cancellationToken);

    if (response is null)
    {
      return Result.Failure<GetInvoiceByIdResponse>(InvoiceErrors.NotFound(query.InvoiceId));
    }

    return Result.Success(response);
  }
}
