namespace Application.Configuration;

public sealed class CampSettings
{
  public const string SectionName = "Camp";

  public TimeOnly CheckOutTime { get; set; }

  public string Name { get; set; } = string.Empty;
  public string Street { get; set; } = string.Empty;
  public string City { get; set; } = string.Empty;
  public string ZipCode { get; set; } = string.Empty;
  public string Cin { get; set; } = string.Empty;
  public string Tin { get; set; } = string.Empty;
  public string Phone { get; set; } = string.Empty;
  public string Email { get; set; } = string.Empty;
  public string Web { get; set; } = string.Empty;
}
