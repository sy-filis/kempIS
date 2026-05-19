namespace Application.Configuration;

public sealed class RetentionSettings
{
  public const string SectionName = "Retention";

  public int GuestYears { get; set; }
  public int BillYears { get; set; }
  public int InvoiceYears { get; set; }
  public TimeOnly RunAtLocalTime { get; set; }
}
