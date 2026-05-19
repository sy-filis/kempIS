using Application.Abstractions.Reservations;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Reservations;

internal sealed class ReservationNumberGenerator(ApplicationDbContext dbContext)
  : IReservationNumberGenerator
{
  public async Task<string> NextAsync(int year, CancellationToken cancellationToken)
  {
    // ReadCommitted may race two callers on the same LastSeq; the unique index on
    // Reservation.Number forces the loser to retry.
    await using IDbContextTransaction tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);

    ReservationNumberSequence? row = await dbContext.Set<ReservationNumberSequence>()
      .FirstOrDefaultAsync(s => s.Year == year, cancellationToken);

    if (row is null)
    {
      row = new ReservationNumberSequence { Year = year, LastSeq = 0 };
      dbContext.Set<ReservationNumberSequence>().Add(row);
    }

    row.LastSeq += 1;
    await dbContext.SaveChangesAsync(cancellationToken);
    await tx.CommitAsync(cancellationToken);

    return $"R-{year}/{row.LastSeq:D4}";
  }
}
