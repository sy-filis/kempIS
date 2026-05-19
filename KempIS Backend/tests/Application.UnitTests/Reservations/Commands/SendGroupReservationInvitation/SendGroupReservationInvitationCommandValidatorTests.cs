using Application.Reservations.Commands.SendGroupReservationInvitation;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Reservations.Commands.SendGroupReservationInvitation;

public sealed class SendGroupReservationInvitationCommandValidatorTests
{
  private readonly SendGroupReservationInvitationCommandValidator _validator = new();

  private static SendGroupReservationInvitationCommand Valid()
      => new(Guid.NewGuid(), "en");

  [Fact]
  public void Valid_Passes()
      => _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();

  [Fact]
  public void GroupReservationId_Empty_Fails()
      => _validator.TestValidate(Valid() with { GroupReservationId = Guid.Empty })
          .ShouldHaveValidationErrorFor(c => c.GroupReservationId);

  [Fact]
  public void Language_Empty_Fails()
      => _validator.TestValidate(Valid() with { Language = string.Empty })
          .ShouldHaveValidationErrorFor(c => c.Language);

  [Fact]
  public void Language_TooLong_Fails()
      => _validator.TestValidate(Valid() with { Language = new string('a', 11) })
          .ShouldHaveValidationErrorFor(c => c.Language);
}
