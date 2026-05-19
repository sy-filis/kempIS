using Application.Finance.Invoices.CreateInvoice;
using Application.Finance.Invoices.Shared;
using Domain.Common;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Finance.Invoices.CreateInvoice;

public sealed class CreateInvoiceCommandValidatorTests
{
  private readonly CreateInvoiceCommandValidator _validator = new();

  private static Address Addr() => new(Guid.NewGuid(), "Prague", "10000", "Main", "1");

  [Fact]
  public void NullTinOnLegalEntity_DoesNotProduceTinError()
  {
    var command = new CreateInvoiceCommand(
      ReservationId: Guid.NewGuid(),
      Payer: null,
      LegalEntity: new InvoiceLegalEntityInput("Acme", "12345678", null, Addr()),
      Email: "billing@example.com",
      PhoneNumber: "+420123456789",
      Items: [new InvoiceItemInput(Guid.NewGuid(), 1m, 100m, 21m)]);

    TestValidationResult<CreateInvoiceCommand> result = _validator.TestValidate(command);

    result.ShouldNotHaveValidationErrorFor(c => c.LegalEntity!.Tin);
  }
}
