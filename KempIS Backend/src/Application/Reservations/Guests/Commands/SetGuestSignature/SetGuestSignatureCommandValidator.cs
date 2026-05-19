using FluentValidation;

namespace Application.Reservations.Guests.Commands.SetGuestSignature;

internal sealed class SetGuestSignatureCommandValidator : AbstractValidator<SetGuestSignatureCommand>
{
  public SetGuestSignatureCommandValidator()
  {
    RuleFor(c => c.Id).NotEmpty();
    RuleFor(c => c.SignaturePngBase64).NotEmpty().ValidPngBase64();
  }
}
