using FluentValidation;

namespace Application.Reservations.ReservationSpotItems.Commands.ReturnKeys;

internal sealed class ReturnKeysCommandValidator : AbstractValidator<ReturnKeysCommand>
{
  public ReturnKeysCommandValidator()
  {
    RuleFor(c => c.Id).NotEmpty();
  }
}
