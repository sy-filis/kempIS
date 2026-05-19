namespace Application.Abstractions.Reservations;

public interface IGroupReservationNumberGenerator
{
  /// <summary>Format: GR-{year}/{seq:D4}.</summary>
  Task<string> NextAsync(int year, CancellationToken cancellationToken);
}
