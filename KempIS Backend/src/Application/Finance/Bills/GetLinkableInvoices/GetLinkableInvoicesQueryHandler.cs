using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Finance.Invoices;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.Bills.GetLinkableInvoices;

internal sealed class GetLinkableInvoicesQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetLinkableInvoicesQuery, IReadOnlyList<LinkableInvoiceView>>
{
  public async Task<Result<IReadOnlyList<LinkableInvoiceView>>> Handle(
    GetLinkableInvoicesQuery query, CancellationToken cancellationToken)
  {
    List<LinkableInvoiceView> list = await context.Invoices
      .AsNoTracking()
      .Where(i => i.ReservationId == query.ReservationId
               && i.Status == InvoiceStatus.Paid
               && i.LinkedBillId == null)
      .Select(i => new LinkableInvoiceView(
        i.Id, i.Number!, i.IssuedAt, i.PaidAt,
        context.InvoiceItems.Where(item => item.InvoiceId == i.Id)
          .Sum(item => item.Quantity * item.UnitPrice)))
      .ToListAsync(cancellationToken);

    return Result.Success<IReadOnlyList<LinkableInvoiceView>>(list);
  }
}
