using Application.Finance.Invoices.Shared;
using Application.Finance.Invoices.UpdateInvoice;
using Domain.Common;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Finance.Invoices.UpdateInvoice;

public sealed class UpdateInvoiceCommandValidatorTests
{
  private readonly UpdateInvoiceCommandValidator _validator = new();

  private static Address Addr() => new(Guid.NewGuid(), "Prague", "10000", "Main", "1");

  [Fact]
  public void NullTinOnLegalEntity_DoesNotProduceTinError()
  {
    var command = new UpdateInvoiceCommand(
      InvoiceId: Guid.NewGuid(),
      Payer: null,
      LegalEntity: new InvoiceLegalEntityInput("Acme", "12345678", null, Addr()),
      Email: "billing@example.com",
      PhoneNumber: "+420123456789",
      Items: [new InvoiceItemInput(Guid.NewGuid(), 1m, 100m, 21m)]);

    TestValidationResult<UpdateInvoiceCommand> result = _validator.TestValidate(command);

    result.ShouldNotHaveValidationErrorFor(c => c.LegalEntity!.Tin);
  }
}
