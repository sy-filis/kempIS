using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Finance.Bills;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Retention;

public sealed record RunBillAnonymizationCommand(DateOnly Today) : ICommand<int>;

internal sealed class RunBillAnonymizationCommandHandler(
  IApplicationDbContext context,
  ILogger<RunBillAnonymizationCommandHandler> logger)
  : ICommandHandler<RunBillAnonymizationCommand, int>
{
  public async Task<Result<int>> Handle(
    RunBillAnonymizationCommand command, CancellationToken cancellationToken)
  {
    List<Guid> dueIds = await context.Bills
      .Where(b => b.Scartation != null && b.Scartation <= command.Today)
      .Select(b => b.Id)
      .ToListAsync(cancellationToken);

    if (dueIds.Count == 0)
    {
      return 0;
    }

    foreach (Guid id in dueIds)
    {
      Bill? bill = await context.Bills.FindAsync([id], cancellationToken);
      if (bill is not null)
      {
        RetentionAnonymizer.Anonymize(bill);
      }
    }

    await context.SaveChangesAsync(cancellationToken);

    if (logger.IsEnabled(LogLevel.Information))
    {
      logger.LogInformation("Anonymized {Count} bills by retention policy.", dueIds.Count);
    }

    return dueIds.Count;
  }
}
