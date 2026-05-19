using Application.Abstractions.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace Infrastructure.Email;

internal sealed partial class SmtpEmailSender(
  IOptions<SmtpOptions> options,
  ILogger<SmtpEmailSender> logger)
  : IEmailSender
{
  private readonly SmtpOptions _options = options.Value;

  public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
  {
    using var mime = new MimeMessage();
    mime.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
    mime.To.Add(MailboxAddress.Parse(message.To));
    mime.Subject = message.Subject;
    mime.Body = new TextPart(message.IsHtml ? TextFormat.Html : TextFormat.Plain)
    {
      Text = message.Body
    };

    using var client = new SmtpClient();

    SecureSocketOptions secureOptions = _options.Security switch
    {
      SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
      SmtpSecurity.StartTls => SecureSocketOptions.StartTls,
      SmtpSecurity.None => SecureSocketOptions.None,
      _ => SecureSocketOptions.Auto
    };

    await client.ConnectAsync(_options.Host, _options.Port, secureOptions, cancellationToken);

    if (!string.IsNullOrEmpty(_options.Username))
    {
      await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
    }

    try
    {
      await client.SendAsync(mime, cancellationToken);
      LogEmailSent(logger, message.To, message.Subject);
    }
    finally
    {
      await client.DisconnectAsync(true, cancellationToken);
    }
  }

  [LoggerMessage(Level = LogLevel.Information, Message = "Email sent to {Recipient} with subject {Subject}")]
  private static partial void LogEmailSent(ILogger logger, string recipient, string subject);
}
