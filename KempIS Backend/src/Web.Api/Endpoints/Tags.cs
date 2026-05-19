namespace Web.Api.Endpoints;

public sealed record Tag(string Name, string Description)
{
  public static implicit operator string(Tag tag) => tag.Name;
}

public static class Tags
{
  public static readonly Tag Addresses = new(
    "Addresses",
    "Address auto-completion / suggestion proxy.");

  public static readonly Tag Auth = new(
    "Auth",
    "Authentication: passkey login, refresh, logout.");

  public static readonly Tag Bills = new(
    "Bills",
    "Bills, repair bills, PDF, sticker, and bills attached to reservations.");

  public static readonly Tag EDoklady = new(
    "eDoklady",
    "Czech e-Doklady fiscal proxy: virtual counters, presentations, transactions.");

  public static readonly Tag Finance = new(
    "Finance",
    "Financial closings and the legal-entity ARES lookup.");

  public static readonly Tag Identity = new(
    "Identity",
    "User and role administration, including passkey registration.");

  public static readonly Tag Invoices = new(
    "Invoices",
    "Invoices, invoice state transitions, and invoices attached to reservations.");

  public static readonly Tag Operations = new(
    "Operations",
    "Out-of-orders, events, cleaning plans, clean infos, maintenance issues, access cards.");

  public static readonly Tag Reception = new(
    "Reception",
    "Reception tablet pairing: issue pair codes, host the WebSocket relay channel.");

  public static readonly Tag Reservations = new(
    "Reservations",
    "Bookings, group reservations, check-in/out, vehicles, meals, guests, spots, spot groups, nationalities, availability.");

  public static readonly Tag Services = new(
    "Services",
    "Service catalogue: services, service types, service groups, service texts, languages, VAT rates.");

  public static readonly Tag Stats = new(
    "Stats",
    "Aggregated read-only statistics: guests by country, service revenue, occupancy, revenue by payment method.");

  public static IEnumerable<Tag> All { get; } = [
    Addresses, Auth, Bills, EDoklady, Finance, Identity, Invoices, Operations, Reception, Reservations, Services, Stats
  ];
}
