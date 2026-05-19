using System.ComponentModel.DataAnnotations;

namespace Application.Abstractions.Reservations;

public sealed class UbyportOptions
{
  public const string SectionName = "Ubyport";

  [Required, Url] public string EndpointUrl { get; set; } = default!;
  [Required] public string Username { get; set; } = default!;
  [Required] public string Password { get; set; } = default!;
  [Required] public string AuthenticationCode { get; set; } = default!;

  [Range(1, int.MaxValue)]
  public int IdUb { get; set; }
  [Required] public string Mark { get; set; } = default!;
  [Required] public string Name { get; set; } = default!;
  [Required] public string Contact { get; set; } = default!;
  [Required] public string District { get; set; } = default!;
  [Required] public string Town { get; set; } = default!;
  public string? TownPart { get; set; }
  [Required] public string Street { get; set; } = default!;
  [Required] public string HouseNumber { get; set; } = default!;
  public string? OrientationNumber { get; set; }
  [Required] public string Zip { get; set; } = default!;
}
