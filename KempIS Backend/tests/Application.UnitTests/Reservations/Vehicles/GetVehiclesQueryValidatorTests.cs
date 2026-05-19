using Application.Reservations.Vehicles;

namespace Application.UnitTests.Reservations.Vehicles;

public sealed class GetVehiclesQueryValidatorTests
{
  private readonly GetVehiclesQueryValidator _validator = new();

  [Fact]
  public void Validate_FromEmpty_IsInvalid()
  {
    GetVehiclesQuery query = new(default, new DateOnly(2026, 5, 5), null);

    FluentValidation.Results.ValidationResult result = _validator.Validate(query);

    result.IsValid.ShouldBeFalse();
    result.Errors.ShouldContain(e => e.PropertyName == nameof(GetVehiclesQuery.From));
  }

  [Fact]
  public void Validate_ToEmpty_IsInvalid()
  {
    GetVehiclesQuery query = new(new DateOnly(2026, 5, 1), default, null);

    FluentValidation.Results.ValidationResult result = _validator.Validate(query);

    result.IsValid.ShouldBeFalse();
    result.Errors.ShouldContain(e => e.PropertyName == nameof(GetVehiclesQuery.To));
  }

  [Fact]
  public void Validate_FromGreaterThanTo_IsInvalid()
  {
    GetVehiclesQuery query = new(new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 1), null);

    FluentValidation.Results.ValidationResult result = _validator.Validate(query);

    result.IsValid.ShouldBeFalse();
    result.Errors.ShouldContain(e => e.PropertyName == nameof(GetVehiclesQuery.To));
  }

  [Fact]
  public void Validate_FromEqualsTo_IsValid()
  {
    GetVehiclesQuery query = new(new DateOnly(2026, 5, 5), new DateOnly(2026, 5, 5), null);

    FluentValidation.Results.ValidationResult result = _validator.Validate(query);

    result.IsValid.ShouldBeTrue();
  }

  [Fact]
  public void Validate_SearchTooLong_IsInvalid()
  {
    string tooLong = new('x', 101);
    GetVehiclesQuery query = new(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5), tooLong);

    FluentValidation.Results.ValidationResult result = _validator.Validate(query);

    result.IsValid.ShouldBeFalse();
    result.Errors.ShouldContain(e => e.PropertyName == nameof(GetVehiclesQuery.Search));
  }

  [Fact]
  public void Validate_SearchNull_IsValid()
  {
    GetVehiclesQuery query = new(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5), null);

    FluentValidation.Results.ValidationResult result = _validator.Validate(query);

    result.IsValid.ShouldBeTrue();
  }

  [Fact]
  public void Validate_SearchEmpty_IsValid()
  {
    GetVehiclesQuery query = new(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5), string.Empty);

    FluentValidation.Results.ValidationResult result = _validator.Validate(query);

    result.IsValid.ShouldBeTrue();
  }

  [Fact]
  public void Validate_SearchAtLengthLimit_IsValid()
  {
    string atLimit = new('x', 100);
    GetVehiclesQuery query = new(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5), atLimit);

    FluentValidation.Results.ValidationResult result = _validator.Validate(query);

    result.IsValid.ShouldBeTrue();
  }
}
