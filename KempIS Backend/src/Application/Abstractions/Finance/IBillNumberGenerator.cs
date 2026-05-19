namespace Application.Abstractions.Finance;

public interface IBillNumberGenerator
{
  /// <summary>Format: {year}/{seq:D4}. Unified sequence across regular and repair bills.</summary>
  Task<string> NextAsync(int year, CancellationToken cancellationToken);
}
