using Application.Abstractions.Finance;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Finance;

internal sealed class BillNumberGenerator(ApplicationDbContext dbContext) : IBillNumberGenerator
{
  public async Task<string> NextAsync(int year, CancellationToken cancellationToken)
  {
    // ReadCommitted may race two callers on the same LastSeq; the unique index on
    // Bill.Number forces the loser to retry.
    await using IDbContextTransaction tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);

    BillNumberSequence? row = await dbContext.Set<BillNumberSequence>()
      .FirstOrDefaultAsync(s => s.Year == year, cancellationToken);

    if (row is null)
    {
      row = new BillNumberSequence { Year = year, LastSeq = 0 };
      dbContext.Set<BillNumberSequence>().Add(row);
    }

    row.LastSeq += 1;
    await dbContext.SaveChangesAsync(cancellationToken);
    await tx.CommitAsync(cancellationToken);

    return $"{year}/{row.LastSeq:D4}";
  }
}
