using Domain.Common;
using SharedKernel;

namespace Application.Abstractions.Reservations;

public interface ISpotAvailabilityChecker
{
  Task<Result> CheckAsync(
    IReadOnlyCollection<Guid> spotIds,
    DateRange period,
    SpotAvailabilityContext context,
    CancellationToken cancellationToken);
}

public sealed record SpotAvailabilityContext(
  Guid? ExcludeReservationId = null,
  Guid? ExcludeGroupReservationId = null,
  Guid? AllowGroupOverlap = null);
