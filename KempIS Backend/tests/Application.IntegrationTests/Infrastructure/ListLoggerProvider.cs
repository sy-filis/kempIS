using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Application.IntegrationTests.Infrastructure;

public sealed class ListLoggerProvider : ILoggerProvider
{
  private readonly ConcurrentQueue<LogEntry> _entries = new();

  public IReadOnlyCollection<LogEntry> Entries => _entries.ToArray();

  public ILogger CreateLogger(string categoryName) => new ListLogger(categoryName, _entries);

  public void Dispose() { }

  public sealed record LogEntry(string Category, LogLevel Level, string Message, Exception? Exception);

  private sealed class ListLogger(string category, ConcurrentQueue<LogEntry> sink) : ILogger
  {
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
      LogLevel logLevel,
      EventId eventId,
      TState state,
      Exception? exception,
      Func<TState, Exception?, string> formatter)
    {
      ArgumentNullException.ThrowIfNull(formatter);
      sink.Enqueue(new LogEntry(category, logLevel, formatter(state, exception), exception));
    }
  }
}
