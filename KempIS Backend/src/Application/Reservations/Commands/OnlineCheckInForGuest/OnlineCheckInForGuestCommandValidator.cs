using Application.Reservations.Guests;
using FluentValidation;

namespace Application.Reservations.Commands.OnlineCheckInForGuest;

internal sealed class OnlineCheckInForGuestCommandValidator : AbstractValidator<OnlineCheckInForGuestCommand>
{
  internal const string VisaNumberPattern = "^([A-Z]{1,3}[0-9]+|BIOMETRIKA)$";

  public OnlineCheckInForGuestCommandValidator()
  {
    RuleFor(c => c.ReservationId).NotEmpty();
    RuleFor(c => c.Secret).NotEmpty();
    RuleFor(c => c.Guests).NotEmpty();
    RuleForEach(c => c.Guests).ChildRules(g =>
    {
      g.RuleFor(x => x.FirstName).NotEmpty().MaximumLength(255);
      g.RuleFor(x => x.LastName).NotEmpty().MaximumLength(255);
      g.RuleFor(x => x.DocumentNumber).MaximumLength(50);
      g.RuleFor(x => x.NationalityId).NotEmpty();
      g.RuleFor(x => x.DocumentType).IsInEnum();
      g.RuleFor(x => x.VisaNumber)
        .Matches(VisaNumberPattern)
        .When(x => !string.IsNullOrEmpty(x.VisaNumber));
      g.RuleFor(x => x.Address).NotNull();
      g.RuleFor(x => x.SignaturePngBase64).ValidPngBase64();
    });
    RuleForEach(c => c.Vehicles).ChildRules(v =>
    {
      v.RuleFor(x => x.RegistrationNumber).NotEmpty().MaximumLength(20);
    });
  }
}
