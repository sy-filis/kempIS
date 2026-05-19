namespace Application.Abstractions.Reservations;

public interface IReservationNumberGenerator
{
  /// <summary>Format: R-{year}/{seq:D4}.</summary>
  Task<string> NextAsync(int year, CancellationToken cancellationToken);
}
