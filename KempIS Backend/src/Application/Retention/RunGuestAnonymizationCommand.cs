using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Reservations.Guests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Retention;

public sealed record RunGuestAnonymizationCommand(DateOnly Today) : ICommand<int>;

internal sealed class RunGuestAnonymizationCommandHandler(
  IApplicationDbContext context,
  ILogger<RunGuestAnonymizationCommandHandler> logger)
  : ICommandHandler<RunGuestAnonymizationCommand, int>
{
  public async Task<Result<int>> Handle(
    RunGuestAnonymizationCommand command, CancellationToken cancellationToken)
  {
    List<Guest> due = await context.Guests
      .Where(g => g.Scartation != null && g.Scartation <= command.Today)
      .ToListAsync(cancellationToken);

    if (due.Count == 0)
    {
      return 0;
    }

    foreach (Guest guest in due)
    {
      RetentionAnonymizer.Anonymize(guest);
    }

    await context.SaveChangesAsync(cancellationToken);

    if (logger.IsEnabled(LogLevel.Information))
    {
      logger.LogInformation("Anonymized {Count} guests by retention policy.", due.Count);
    }

    return due.Count;
  }
}
