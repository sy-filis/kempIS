namespace Infrastructure.Email;

public sealed class SmtpOptions
{
  public const string SectionName = "Smtp";

  public string Host { get; set; } = string.Empty;

  public int Port { get; set; } = 587;

  public string Username { get; set; } = string.Empty;

  public string Password { get; set; } = string.Empty;

  public string FromAddress { get; set; } = string.Empty;

  public string FromName { get; set; } = string.Empty;

  public SmtpSecurity Security { get; set; } = SmtpSecurity.StartTls;
}

public enum SmtpSecurity
{
  None,
  SslOnConnect,
  StartTls,
}
