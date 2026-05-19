using Application.Reservations.Commands.UpdateGroupReservation;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Reservations.Commands.UpdateGroupReservation;

public sealed class UpdateGroupReservationCommandValidatorTests
{
  private readonly UpdateGroupReservationCommandValidator _validator = new();

  private static UpdateGroupReservationCommand Valid() => new(
    Id: Guid.NewGuid(),
    From: new DateOnly(2026, 7, 1),
    To: new DateOnly(2026, 7, 5),
    SpotIds: [Guid.NewGuid()],
    OrganizerName: "Alice",
    OrganizerEmail: "alice@example.com",
    OrganizerPhone: "+420777111222",
    Note: null);

  [Fact]
  public void Valid_Passes() => _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();

  [Fact]
  public void DisplayName_NullAllowed()
      => _validator.TestValidate(Valid() with { DisplayName = null })
          .ShouldNotHaveValidationErrorFor(c => c.DisplayName);

  [Fact]
  public void DisplayName_AtMax_Passes()
      => _validator.TestValidate(Valid() with { DisplayName = new string('x', 100) })
          .ShouldNotHaveValidationErrorFor(c => c.DisplayName);

  [Fact]
  public void DisplayName_TooLong_Fails()
      => _validator.TestValidate(Valid() with { DisplayName = new string('x', 101) })
          .ShouldHaveValidationErrorFor(c => c.DisplayName);
}
