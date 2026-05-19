using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Finance.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Retention;

public sealed record RunInvoiceAnonymizationCommand(DateOnly Today) : ICommand<int>;

internal sealed class RunInvoiceAnonymizationCommandHandler(
  IApplicationDbContext context,
  ILogger<RunInvoiceAnonymizationCommandHandler> logger)
  : ICommandHandler<RunInvoiceAnonymizationCommand, int>
{
  public async Task<Result<int>> Handle(
    RunInvoiceAnonymizationCommand command, CancellationToken cancellationToken)
  {
    List<Invoice> due = await context.Invoices
      .Where(i => i.Scartation != null && i.Scartation <= command.Today)
      .ToListAsync(cancellationToken);

    if (due.Count == 0)
    {
      return 0;
    }

    foreach (Invoice invoice in due)
    {
      RetentionAnonymizer.Anonymize(invoice);
    }

    await context.SaveChangesAsync(cancellationToken);

    if (logger.IsEnabled(LogLevel.Information))
    {
      logger.LogInformation("Anonymized {Count} invoices by retention policy.", due.Count);
    }

    return due.Count;
  }
}
