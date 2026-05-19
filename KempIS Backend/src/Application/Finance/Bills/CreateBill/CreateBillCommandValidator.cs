using FluentValidation;

namespace Application.Finance.Bills.CreateBill;

internal sealed class CreateBillCommandValidator : AbstractValidator<CreateBillCommand>
{
  public CreateBillCommandValidator()
  {
    RuleFor(c => c.CheckInAt).LessThanOrEqualTo(c => c.CheckOutAt);

    RuleFor(c => c.Payer).NotNull();
    RuleFor(c => c.Payer.Name).NotEmpty().MaximumLength(255);
    RuleFor(c => c.Payer.Surname).NotEmpty().MaximumLength(255);

    When(c => c.LegalEntity is not null, () =>
    {
      RuleFor(c => c.LegalEntity!.Name).NotEmpty().MaximumLength(255);
      RuleFor(c => c.LegalEntity!.Cin).NotEmpty();
    });

    RuleFor(c => c.LanguageId).NotEmpty();

    RuleFor(c => c.Items).NotEmpty();
    RuleForEach(c => c.Items).ChildRules(item =>
    {
      item.RuleFor(i => i.Quantity).GreaterThan(0u);
      item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0m);
      // Catalogue-service VAT is snapshotted by the handler; only ad-hoc items use this field.
      item.RuleFor(i => i.VatRatePercentage)
        .InclusiveBetween(0m, 100m)
        .When(i => i.ServiceId is null);
    });

    RuleFor(c => c)
      .Must(c => c.ReservationId.HasValue || c.LinkedInvoiceIds.Count == 0)
      .WithErrorCode("Bill.WalkInCannotLinkInvoices")
      .WithMessage("A bill without a reservation cannot link any invoices.");

    RuleFor(c => c.LinkedInvoiceIds)
      .Must(ids => ids.Distinct().Count() == ids.Count)
      .WithErrorCode("Bill.DuplicateInvoiceIds")
      .WithMessage("LinkedInvoiceIds must not contain duplicates.");

    RuleFor(c => c.ExistingGuests)
      .Must(items => items.Select(i => i.GuestId).Distinct().Count() == items.Count)
      .WithErrorCode("Bill.DuplicateGuestIds")
      .WithMessage("ExistingGuests must not contain duplicate guest ids.");

    RuleForEach(c => c.ExistingGuests).ChildRules(g =>
    {
      g.RuleFor(x => x.GuestId).NotEmpty();
    });

    RuleForEach(c => c.NewGuests).ChildRules(g =>
    {
      g.RuleFor(x => x.FirstName).NotEmpty();
      g.RuleFor(x => x.LastName).NotEmpty();
      g.RuleFor(x => x.NationalityId).NotEmpty();
      g.RuleFor(x => x.DocumentNumber).NotEmpty();
      g.RuleFor(x => x.ReasonOfStay).NotEmpty();
    });

    RuleFor(c => c.ReservationSpotItemIds)
      .Must(ids => ids.Distinct().Count() == ids.Count)
      .WithErrorCode("Bill.DuplicateSpotItemIds")
      .WithMessage("ReservationSpotItemIds must not contain duplicates.");

    RuleFor(c => c)
      .Must(c => c.ReservationId.HasValue || c.ReservationSpotItemIds.Count == 0)
      .WithErrorCode("Bill.WalkInCannotLinkSpotItems")
      .WithMessage("A bill without a reservation cannot link any spot items.");

    RuleForEach(c => c.AccessCards).ChildRules(card =>
    {
      card.RuleFor(x => x.Uid).GreaterThan(0UL);
      card.RuleFor(x => x.Deposit).GreaterThanOrEqualTo(0m);
      card.RuleFor(x => x.ValidUntil).NotEqual(default(DateOnly));
    });

    RuleFor(c => c.AccessCards)
      .Must(cards => cards.Select(x => x.Uid).Distinct().Count() == cards.Count)
      .WithErrorCode("Bill.DuplicateAccessCardUids")
      .WithMessage("AccessCards must not contain duplicate UIDs.");

    RuleForEach(c => c.NewVehicles).ChildRules(v =>
    {
      v.RuleFor(x => x.RegistrationNumber).NotEmpty().MaximumLength(20);
      v.RuleFor(x => x.ServiceId).NotEmpty();
    });

    RuleFor(c => c.ExistingVehicleIds)
      .Must(ids => ids.Distinct().Count() == ids.Count)
      .WithErrorCode("Bill.DuplicateVehicleIds")
      .WithMessage("ExistingVehicleIds must not contain duplicates.");
  }
}
