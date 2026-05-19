using SharedKernel;

namespace Application.Abstractions.Reservations;

public interface IPoliceGuestReporter
{
  Task<Result> SubmitAsync(
    IReadOnlyCollection<PoliceGuestEntry> entries,
    CancellationToken cancellationToken);
}
