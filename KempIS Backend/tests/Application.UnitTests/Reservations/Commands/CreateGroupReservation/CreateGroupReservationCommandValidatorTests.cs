using Application.Reservations.Commands.CreateGroupReservation;
using Domain.Reservations;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Reservations.Commands.CreateGroupReservation;

public sealed class CreateGroupReservationCommandValidatorTests
{
  private readonly CreateGroupReservationCommandValidator _validator = new();

  private static CreateGroupReservationCommand Valid() => new(
      From: new DateOnly(2026, 7, 1),
      To: new DateOnly(2026, 7, 10),
      SpotIds: [Guid.NewGuid()],
      OrganizerName: "Org",
      OrganizerEmail: "org@example.com",
      OrganizerPhone: "+420 777 123 456",
      Note: null,
      Language: ReservationLanguages.Czech);

  [Fact]
  public void Valid_Passes()
      => _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();

  [Fact]
  public void To_BeforeFrom_Fails()
      => _validator.TestValidate(Valid() with
      {
        From = new DateOnly(2026, 7, 5),
        To = new DateOnly(2026, 7, 3),
      })
          .ShouldHaveValidationErrorFor(c => c.To);

  [Fact]
  public void SpotIds_Empty_Fails()
      => _validator.TestValidate(Valid() with { SpotIds = [] })
          .ShouldHaveValidationErrorFor(c => c.SpotIds);

  [Fact]
  public void SpotIds_ContainsEmptyGuid_Fails()
      => _validator.TestValidate(Valid() with { SpotIds = [Guid.Empty] })
          .ShouldHaveValidationErrorFor("SpotIds[0]");

  [Fact]
  public void OrganizerName_Empty_Fails()
      => _validator.TestValidate(Valid() with { OrganizerName = string.Empty })
          .ShouldHaveValidationErrorFor(c => c.OrganizerName);

  [Fact]
  public void OrganizerName_TooLong_Fails()
      => _validator.TestValidate(Valid() with { OrganizerName = new string('a', 257) })
          .ShouldHaveValidationErrorFor(c => c.OrganizerName);

  [Fact]
  public void OrganizerEmail_InvalidFormat_Fails()
      => _validator.TestValidate(Valid() with { OrganizerEmail = "not-an-email" })
          .ShouldHaveValidationErrorFor(c => c.OrganizerEmail);

  [Fact]
  public void OrganizerPhone_Empty_Fails()
      => _validator.TestValidate(Valid() with { OrganizerPhone = string.Empty })
          .ShouldHaveValidationErrorFor(c => c.OrganizerPhone);

  [Fact]
  public void OrganizerPhone_TooLong_Fails()
      => _validator.TestValidate(Valid() with { OrganizerPhone = new string('1', 51) })
          .ShouldHaveValidationErrorFor(c => c.OrganizerPhone);

  [Fact]
  public void Note_TooLong_Fails()
      => _validator.TestValidate(Valid() with { Note = new string('x', 1001) })
          .ShouldHaveValidationErrorFor(c => c.Note);

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

  [Theory]
  [InlineData("cs")]
  [InlineData("en")]
  public void Language_KnownValue_Passes(string language)
      => _validator.TestValidate(Valid() with { Language = language })
          .ShouldNotHaveValidationErrorFor(c => c.Language);

  [Fact]
  public void Language_Empty_Fails()
      => _validator.TestValidate(Valid() with { Language = string.Empty })
          .ShouldHaveValidationErrorFor(c => c.Language);

  [Fact]
  public void Language_Unknown_Fails()
      => _validator.TestValidate(Valid() with { Language = "fr" })
          .ShouldHaveValidationErrorFor(c => c.Language);
}
