using System.Collections.Concurrent;
using Application.Abstractions.Email;

namespace TestUtilities.Fakes;

public sealed class CapturingEmailSender : IEmailSender
{
  private readonly ConcurrentQueue<EmailMessage> _sent = new();

  public IReadOnlyCollection<EmailMessage> Sent => _sent.ToArray();

  public EmailMessage Only => _sent.Count == 1
    ? _sent.First()
    : throw new InvalidOperationException($"Expected exactly one sent email, found {_sent.Count}.");

  public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
  {
    _sent.Enqueue(message);
    return Task.CompletedTask;
  }

  public void Clear() => _sent.Clear();
}
