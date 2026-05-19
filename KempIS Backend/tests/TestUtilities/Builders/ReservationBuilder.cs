using Domain.Common;
using Domain.Reservations;
using Domain.Reservations.ReservationMakers;
using Domain.Reservations.Reservations;
using Domain.Reservations.ReservationStates;
using TestUtilities.Fakes;

namespace TestUtilities.Builders;

public sealed class ReservationBuilder
{
  private Guid _id = Guid.NewGuid();
  private DateRange _period = new(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5));
  private ReservationState _state = ReservationState.Confirmed;
  private ReservationMaker _maker = new("Jan", "Novak", "jan@example.com", "+420000000000");
  private Guid? _groupId;
  private DateTime _createdUtc = FakeDateTimeProvider.DefaultUtc;
  private DateTime? _updatedUtc;
  private string? _note;
  private string _secret = new('0', 64);
  private string? _number;
  private string _language = ReservationLanguages.Czech;

  public ReservationBuilder WithId(Guid id) { _id = id; return this; }
  public ReservationBuilder WithNumber(string number) { _number = number; return this; }
  public ReservationBuilder For(DateOnly from, DateOnly to) { _period = new DateRange(from, to); return this; }
  public ReservationBuilder For(DateRange period) { _period = period; return this; }
  public ReservationBuilder InState(ReservationState state) { _state = state; return this; }
  public ReservationBuilder InGroup(Guid groupId) { _groupId = groupId; return this; }
  public ReservationBuilder MadeBy(ReservationMaker maker) { _maker = maker; return this; }
  public ReservationBuilder MadeBy(string name, string surname, string email, string phone)
  {
    _maker = new ReservationMaker(name, surname, email, phone);
    return this;
  }
  public ReservationBuilder CreatedAt(DateTime utc) { _createdUtc = utc; return this; }
  public ReservationBuilder UpdatedAt(DateTime utc) { _updatedUtc = utc; return this; }
  public ReservationBuilder WithNote(string? note) { _note = note; return this; }
  public ReservationBuilder WithSecret(string secret) { _secret = secret; return this; }
  public ReservationBuilder WithLanguage(string language) { _language = language; return this; }

  public Reservation Build() => new()
  {
    Id = _id,
    Number = _number ?? $"R-TEST/{_id.ToString("N")[..8]}",
    Period = _period,
    State = _state,
    ReservationMaker = _maker,
    GroupReservationId = _groupId,
    CreatedAtUtc = _createdUtc,
    UpdatedAtUtc = _updatedUtc,
    Note = _note,
    Secret = _secret,
    Language = _language,
  };
}
