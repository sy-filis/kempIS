using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Application.Abstractions.Data;

public partial interface IApplicationDbContext
{
  Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

  EntityEntry Entry(object entity);
}
