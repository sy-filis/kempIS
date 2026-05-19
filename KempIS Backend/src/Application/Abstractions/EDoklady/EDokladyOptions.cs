using System.ComponentModel.DataAnnotations;

namespace Application.Abstractions.EDoklady;

public sealed class EDokladyOptions
{
  public const string SectionName = "EDoklady";

  [Required, Url] public string BaseUrl { get; set; } = default!;

  [Required] public CertificateOptions Certificate { get; set; } = new();

  [Range(1, 365)] public int QrCodeRefreshThresholdDays { get; set; } = 7;

  public GeolocationOptions? Geolocation { get; set; }

  public sealed class CertificateOptions
  {
    [Required] public string PfxPath { get; set; } = default!;
    [Required] public string PfxPassword { get; set; } = default!;
  }

  public sealed class GeolocationOptions
  {
    [Range(-90.0, 90.0)] public double Latitude { get; set; }
    [Range(-180.0, 180.0)] public double Longitude { get; set; }
    [Range(1, int.MaxValue)] public int ToleranceInMeters { get; set; }
  }
}
