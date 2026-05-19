namespace Application.Reception.Realtime;

public enum PeerRole
{
  Desktop = 1,
  Tablet = 2,
}

public static class PeerRoleExtensions
{
  public static string ToWireString(this PeerRole role) => role switch
  {
    PeerRole.Desktop => "desktop",
    PeerRole.Tablet => "tablet",
    _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
  };

  public static PeerRole? FromWireString(string? value) => value switch
  {
    "desktop" => PeerRole.Desktop,
    "tablet" => PeerRole.Tablet,
    _ => null,
  };
}
