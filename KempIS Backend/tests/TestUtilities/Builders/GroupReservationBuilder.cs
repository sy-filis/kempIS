using Domain.Common;
using Domain.Reservations.GroupReservations;
using TestUtilities.Fakes;

namespace TestUtilities.Builders;

public sealed class GroupReservationBuilder
{
  private static int _numberCounter;

  private Guid _id = Guid.NewGuid();
  private string _number = $"GR-TEST/{Interlocked.Increment(ref _numberCounter):D4}";
  private DateRange _period = new(new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 20));
  private GroupReservationState _state = GroupReservationState.Confirmed;
  private string _secret = "00000000000000000000000000000000";
  private string _organizerName = "Organizer";
  private string _organizerEmail = "organizer@example.com";
  private string _organizerPhone = "+420 000 000 000";
  private DateTime _createdUtc = FakeDateTimeProvider.DefaultUtc;
  private DateTime? _updatedUtc;
  private string? _note;
  private string _language = "cs";
  private readonly List<GroupReservationSpot> _heldSpots = [];

  public GroupReservationBuilder WithId(Guid id) { _id = id; return this; }
  public GroupReservationBuilder WithNumber(string number) { _number = number; return this; }
  public GroupReservationBuilder For(DateOnly from, DateOnly to) { _period = new DateRange(from, to); return this; }
  public GroupReservationBuilder InState(GroupReservationState state) { _state = state; return this; }
  public GroupReservationBuilder WithSecret(string secret) { _secret = secret; return this; }
  public GroupReservationBuilder WithOrganizer(string name, string email)
  {
    _organizerName = name;
    _organizerEmail = email;
    return this;
  }
  public GroupReservationBuilder WithOrganizerPhone(string phone) { _organizerPhone = phone; return this; }
  public GroupReservationBuilder CreatedAt(DateTime utc) { _createdUtc = utc; return this; }
  public GroupReservationBuilder UpdatedAt(DateTime utc) { _updatedUtc = utc; return this; }
  public GroupReservationBuilder WithNote(string? note) { _note = note; return this; }
  public GroupReservationBuilder WithLanguage(string language) { _language = language; return this; }
  public GroupReservationBuilder HoldingSpots(params Guid[] spotIds)
  {
    foreach (Guid spotId in spotIds)
    {
      _heldSpots.Add(new GroupReservationSpot { GroupReservationId = _id, SpotId = spotId });
    }
    return this;
  }

  public GroupReservation Build() => new()
  {
    Id = _id,
    Number = _number,
    Period = _period,
    State = _state,
    Secret = _secret,
    OrganizerName = _organizerName,
    OrganizerEmail = _organizerEmail,
    OrganizerPhone = _organizerPhone,
    CreatedAtUtc = _createdUtc,
    UpdatedAtUtc = _updatedUtc,
    Note = _note,
    Language = _language,
    HeldSpots = [.. _heldSpots.Select(h => new GroupReservationSpot
    {
      GroupReservationId = _id,
      SpotId = h.SpotId,
    })],
  };
}
