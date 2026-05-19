using Application.Finance.Bills.CreateBill;
using Application.Finance.Bills.Shared;
using Domain.Common;
using Domain.Finance.Payments;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Finance.Bills.CreateBill;

public sealed class CreateBillCommandValidatorTests
{
  private readonly CreateBillCommandValidator _validator = new();

  private static Address Addr() => new(Guid.NewGuid(), "Prague", "10000", "Main", "1");

  [Fact]
  public void NullTinOnLegalEntity_DoesNotProduceTinError()
  {
    var command = new CreateBillCommand(
      ReservationId: Guid.NewGuid(),
      CheckInAt: new DateOnly(2026, 4, 20),
      CheckOutAt: new DateOnly(2026, 4, 22),
      Payer: new BillPayerInput("John", "Doe", Addr()),
      LegalEntity: new BillLegalEntityInput("Acme", "12345678", null, Addr()),
      PaymentType: PaymentType.Card,
      LanguageId: Guid.NewGuid(),
      Items: [new BillItemInput(Guid.NewGuid(), 1u, 100m, 21m, 1u, 1u)],
      LinkedInvoiceIds: [],
      ExistingGuests: [],
      NewGuests: [],
      ReservationSpotItemIds: [],
      AccessCards: [],
      NewVehicles: [],
      ExistingVehicleIds: []);

    TestValidationResult<CreateBillCommand> result = _validator.TestValidate(command);

    result.ShouldNotHaveValidationErrorFor(c => c.LegalEntity!.Tin);
  }

  [Fact]
  public void NoGuests_DoesNotProduceModelLevelError()
  {
    var command = new CreateBillCommand(
      ReservationId: Guid.NewGuid(),
      CheckInAt: new DateOnly(2026, 4, 20),
      CheckOutAt: new DateOnly(2026, 4, 22),
      Payer: new BillPayerInput("John", "Doe", Addr()),
      LegalEntity: null,
      PaymentType: PaymentType.Card,
      LanguageId: Guid.NewGuid(),
      Items: [new BillItemInput(Guid.NewGuid(), 1u, 100m, 21m, 1u, 1u)],
      LinkedInvoiceIds: [],
      ExistingGuests: [],
      NewGuests: [],
      ReservationSpotItemIds: [],
      AccessCards: [],
      NewVehicles: [],
      ExistingVehicleIds: []);

    TestValidationResult<CreateBillCommand> result = _validator.TestValidate(command);

    result.Errors.ShouldNotContain(e => e.ErrorCode == "Bill.MustHaveAtLeastOneGuest");
  }
}
