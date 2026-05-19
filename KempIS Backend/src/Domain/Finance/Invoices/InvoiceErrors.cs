using SharedKernel;

namespace Domain.Finance.Invoices;

public static class InvoiceErrors
{
  public static Error NotFound(Guid invoiceId) => Error.NotFound(
      "Invoice.NotFound",
      $"The Invoice with the Id = '{invoiceId}' was not found");

  public static readonly Error NotDraft =
    Error.Conflict("Invoice.NotDraft", "Operation requires the invoice to be in Draft status.");

  public static readonly Error NotCreated =
    Error.Conflict("Invoice.NotCreated", "Operation requires the invoice to be in Created status.");

  public static Error NumberAlreadyUsed(string number) =>
    Error.Conflict("Invoice.NumberAlreadyUsed",
      $"Invoice number '{number}' is already used.");

  public static readonly Error AlreadyLinkedToBill =
    Error.Conflict("Invoice.AlreadyLinkedToBill",
      "Invoice is already linked to another bill.");

  public static readonly Error NotPaid =
    Error.Conflict("Invoice.NotPaid", "Invoice must be in Paid status to link to a bill.");

  public static readonly Error ReservationMismatch =
    Error.Problem("Invoice.ReservationMismatch",
      "Invoice and bill must belong to the same reservation.");

  public static readonly Error PayerOrLegalEntityRequired =
    Error.Problem("Invoice.PayerOrLegalEntityRequired",
      "Invoice must have either a Payer or a LegalEntity, but not both.");
}
