using Domain.Finance.BillItems;
using Domain.Finance.Bills;
using Domain.Finance.FinancialClosings;
using Domain.Finance.InvoiceItems;
using Domain.Finance.Invoices;
using Microsoft.EntityFrameworkCore;

namespace Application.Abstractions.Data;

public partial interface IApplicationDbContext
{
  DbSet<BillItem> BillItems { get; }
  DbSet<Bill> Bills { get; }
  DbSet<FinancialClosing> FinancialClosings { get; }
  DbSet<InvoiceItem> InvoiceItems { get; }
  DbSet<Invoice> Invoices { get; }
}
