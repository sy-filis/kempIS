using Application.Reservations.Commands.CreateWebReservation;
using Domain.Reservations;
using FluentValidation.TestHelper;
using TestUtilities.Fakes;

namespace Application.UnitTests.Reservations.Commands.CreateWebReservation;

public sealed class CreateWebReservationCommandValidatorTests
{
  private readonly CreateWebReservationCommandValidator _validator = new(new FakeDateTimeProvider());

  private static CreateWebReservationCommand Valid() => new(
      Name: "Web",
      Surname: "Guest",
      Email: "web@example.com",
      Phone: "+420123",
      From: new DateOnly(2026, 7, 10),
      To: new DateOnly(2026, 7, 15),
      RequestedSpots: [new RequestedSpotGroup(Guid.NewGuid(), 1)],
      Note: null,
      GroupReservationId: null,
      GroupReservationSecret: null);

  [Fact]
  public void Valid_Passes()
      => _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();

  [Fact]
  public void Email_InvalidFormat_Fails()
      => _validator.TestValidate(Valid() with { Email = "nope" })
          .ShouldHaveValidationErrorFor(c => c.Email);

  [Fact]
  public void To_BeforeFrom_Fails()
      => _validator.TestValidate(Valid() with
      {
        From = new DateOnly(2026, 7, 15),
        To = new DateOnly(2026, 7, 10),
      })
          .ShouldHaveValidationErrorFor(c => c.To);

  [Fact]
  public void RequestedSpots_Empty_Fails()
      => _validator.TestValidate(Valid() with { RequestedSpots = [] })
          .ShouldHaveValidationErrorFor(c => c.RequestedSpots);

  [Fact]
  public void RequestedSpot_EmptySpotGroupId_Fails()
      => _validator.TestValidate(
          Valid() with { RequestedSpots = [new RequestedSpotGroup(Guid.Empty, 1)] })
          .ShouldHaveValidationErrorFor("RequestedSpots[0].SpotGroupId");

  [Fact]
  public void RequestedSpot_ZeroQuantity_Fails()
      => _validator.TestValidate(
          Valid() with { RequestedSpots = [new RequestedSpotGroup(Guid.NewGuid(), 0)] })
          .ShouldHaveValidationErrorFor("RequestedSpots[0].Quantity");

  [Fact]
  public void Note_TooLong_Fails()
      => _validator.TestValidate(Valid() with { Note = new string('x', 1001) })
          .ShouldHaveValidationErrorFor(c => c.Note);

  [Fact]
  public void Language_Null_IsValid()
      => _validator.TestValidate(Valid() with { Language = null })
          .ShouldNotHaveValidationErrorFor(c => c.Language);

  [Theory]
  [InlineData("cs")]
  [InlineData("en")]
  public void Language_KnownValue_IsValid(string language)
      => _validator.TestValidate(Valid() with { Language = language })
          .ShouldNotHaveValidationErrorFor(c => c.Language);

  [Fact]
  public void Language_UnknownValue_IsInvalid()
      => _validator.TestValidate(Valid() with { Language = "fr" })
          .ShouldHaveValidationErrorFor(c => c.Language);
}
