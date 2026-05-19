using SharedKernel;

namespace Domain.Finance.Bills;

public static class BillErrors
{
  public static Error NotFound(Guid billId) => Error.NotFound(
      "Bill.NotFound",
      $"The Bill with the Id = '{billId}' was not found");

  public static readonly Error OriginalMustBeRegular =
    Error.Conflict("Bill.OriginalMustBeRegular",
      "The original bill of a repair bill must be a regular bill.");

  public static readonly Error RepairLineNotOnOriginal =
    Error.Problem("Bill.RepairLineNotOnOriginal",
      "A repair-bill line must match a line on the original bill.");

  public static Error RepairQuantityExceedsCap(decimal attempted, decimal cap) =>
    Error.Problem("Bill.RepairQuantityExceedsCap",
      $"Repair quantity {attempted} exceeds the remaining cap of {cap} for a matching original line.");

  public static readonly Error WalkInCannotLinkInvoices =
    Error.Problem("Bill.WalkInCannotLinkInvoices",
      "A bill without a reservation cannot link any invoices.");

  public static readonly Error GuestAlreadyLinkedToAnotherBill =
    Error.Conflict("Bill.GuestAlreadyLinkedToAnotherBill",
      "One or more supplied guests are already linked to another bill.");

  public static readonly Error DeductionsExceedItemsTotal =
    Error.Problem("Bill.DeductionsExceedItemsTotal",
      "Linked invoice totals exceed the bill's item total. A refund requires a separate document.");

  public static readonly Error DuplicateInvoiceIds =
    Error.Problem("Bill.DuplicateInvoiceIds",
      "LinkedInvoiceIds must not contain duplicates.");

  public static readonly Error DuplicateGuestIds =
    Error.Problem("Bill.DuplicateGuestIds",
      "ExistingGuestIds must not contain duplicates.");

  public static readonly Error SpotItemAlreadyLinkedToAnotherBill =
    Error.Conflict("Bill.SpotItemAlreadyLinkedToAnotherBill",
      "One or more supplied spot items are already linked to another bill.");

  public static readonly Error SpotItemNotInReservation =
    Error.Problem("Bill.SpotItemNotInReservation",
      "One or more supplied spot items do not belong to the bill's reservation.");

  public static readonly Error DuplicateSpotItemIds =
    Error.Problem("Bill.DuplicateSpotItemIds",
      "ReservationSpotItemIds must not contain duplicates.");

  public static readonly Error DuplicateAccessCardUids =
    Error.Problem("Bill.DuplicateAccessCardUids",
      "AccessCards must not contain duplicate UIDs.");

  public static readonly Error DuplicateVehicleIds =
    Error.Problem("Bill.DuplicateVehicleIds",
      "ExistingVehicleIds must not contain duplicates.");

  public static readonly Error VehicleAlreadyLinkedToAnotherBill =
    Error.Conflict("Bill.VehicleAlreadyLinkedToAnotherBill",
      "One or more supplied vehicles are already linked to another bill.");

  public static readonly Error VehicleNotInReservation =
    Error.Problem("Bill.VehicleNotInReservation",
      "One or more supplied vehicles do not belong to the bill's reservation.");

  public static readonly Error WalkInCannotLinkSpotItems =
    Error.Problem("Bill.WalkInCannotLinkSpotItems",
      "A bill without a reservation cannot link any spot items.");

  public static Error AlreadyInClosing(Guid billId) => Error.Conflict(
      "Bills.AlreadyInClosing",
      $"The Bill with the Id = '{billId}' is part of a financial closing and cannot be modified or deleted.");
}
