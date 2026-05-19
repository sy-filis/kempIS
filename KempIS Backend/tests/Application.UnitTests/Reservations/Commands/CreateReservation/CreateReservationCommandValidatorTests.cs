using Application.Reservations.Commands.CreateReservation;
using Domain.Reservations;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Reservations.Commands.CreateReservation;

public sealed class CreateReservationCommandValidatorTests
{
  private readonly CreateReservationCommandValidator _validator = new();

  private static CreateReservationCommand Valid() => new(
      Name: "Jan",
      Surname: "Novak",
      Email: "jan@example.com",
      Phone: "+420000",
      From: new DateOnly(2026, 6, 1),
      To: new DateOnly(2026, 6, 3),
      SpotIds: [Guid.NewGuid()],
      Note: null,
      GroupReservationId: null,
      Services: Array.Empty<ReservationServiceLine>(),
      Vehicles: Array.Empty<ReservationVehicleLine>());

  [Fact]
  public void Valid_Passes() => _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();

  [Fact]
  public void Name_Empty_Fails()
      => _validator.TestValidate(Valid() with { Name = string.Empty })
          .ShouldHaveValidationErrorFor(c => c.Name);

  [Fact]
  public void Name_TooLong_Fails()
      => _validator.TestValidate(Valid() with { Name = new string('a', 256) })
          .ShouldHaveValidationErrorFor(c => c.Name);

  [Fact]
  public void Surname_Empty_Fails()
      => _validator.TestValidate(Valid() with { Surname = string.Empty })
          .ShouldHaveValidationErrorFor(c => c.Surname);

  [Fact]
  public void Email_Empty_Fails()
      => _validator.TestValidate(Valid() with { Email = string.Empty })
          .ShouldHaveValidationErrorFor(c => c.Email);

  [Fact]
  public void Email_InvalidFormat_Fails()
      => _validator.TestValidate(Valid() with { Email = "not-an-email" })
          .ShouldHaveValidationErrorFor(c => c.Email);

  [Fact]
  public void Phone_Empty_Fails()
      => _validator.TestValidate(Valid() with { Phone = string.Empty })
          .ShouldHaveValidationErrorFor(c => c.Phone);

  [Fact]
  public void Phone_TooLong_Fails()
      => _validator.TestValidate(Valid() with { Phone = new string('1', 51) })
          .ShouldHaveValidationErrorFor(c => c.Phone);

  [Fact]
  public void To_BeforeFrom_Fails()
      => _validator.TestValidate(Valid() with
      {
        From = new DateOnly(2026, 6, 5),
        To = new DateOnly(2026, 6, 3),
      })
          .ShouldHaveValidationErrorFor(c => c.To);

  [Fact]
  public void To_EqualToFrom_Fails()
      => _validator.TestValidate(Valid() with
      {
        From = new DateOnly(2026, 6, 5),
        To = new DateOnly(2026, 6, 5),
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
  public void Note_NullAllowed()
      => _validator.TestValidate(Valid() with { Note = null })
          .ShouldNotHaveValidationErrorFor(c => c.Note);

  [Fact]
  public void Note_AtMax_Passes()
      => _validator.TestValidate(Valid() with { Note = new string('x', 1000) })
          .ShouldNotHaveValidationErrorFor(c => c.Note);

  [Fact]
  public void Note_TooLong_Fails()
      => _validator.TestValidate(Valid() with { Note = new string('x', 1001) })
          .ShouldHaveValidationErrorFor(c => c.Note);

  [Fact]
  public void Services_DuplicateServiceIds_Fails()
  {
    var serviceId = Guid.NewGuid();
    _validator.TestValidate(Valid() with
    {
      Services =
      [
        new ReservationServiceLine(serviceId, 1u, 0u, 0u),
        new ReservationServiceLine(serviceId, 1u, 0u, 0u),
      ],
    })
        .ShouldHaveValidationErrorFor(c => c.Services);
  }

  [Fact]
  public void Services_EmptyServiceId_Fails()
  {
    _validator.TestValidate(Valid() with
    {
      Services = [new ReservationServiceLine(Guid.Empty, 1u, 0u, 0u)],
    })
        .ShouldHaveValidationErrorFor("Services[0].ServiceId");
  }

  [Fact]
  public void Vehicles_EmptyPlate_Fails()
  {
    _validator.TestValidate(Valid() with
    {
      Vehicles = [new ReservationVehicleLine(Id: null, RegistrationNumber: string.Empty)],
    })
        .ShouldHaveValidationErrorFor("Vehicles[0].RegistrationNumber");
  }

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
