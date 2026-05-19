using Application.Abstractions.Reservations;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Reservations;

internal sealed class GroupReservationNumberGenerator(ApplicationDbContext dbContext)
  : IGroupReservationNumberGenerator
{
  public async Task<string> NextAsync(int year, CancellationToken cancellationToken)
  {
    // ReadCommitted may race two callers on the same LastSeq; the unique index on
    // GroupReservation.Number forces the loser to retry.
    await using IDbContextTransaction tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);

    GroupReservationNumberSequence? row = await dbContext.Set<GroupReservationNumberSequence>()
      .FirstOrDefaultAsync(s => s.Year == year, cancellationToken);

    if (row is null)
    {
      row = new GroupReservationNumberSequence { Year = year, LastSeq = 0 };
      dbContext.Set<GroupReservationNumberSequence>().Add(row);
    }

    row.LastSeq += 1;
    await dbContext.SaveChangesAsync(cancellationToken);
    await tx.CommitAsync(cancellationToken);

    return $"GR-{year}/{row.LastSeq:D4}";
  }
}
