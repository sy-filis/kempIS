using Domain.Reservations.Guests;
using SharedKernel;

namespace Application.Reservations.Commands.OnlineCheckInForGuest;

public static class OnlineCheckInErrors
{
  public static Error SignatureRequired(int guestIndex) => Error.Problem(
    "OnlineCheckIn.SignatureRequired",
    $"A signature is required for guest at index {guestIndex}.");

  public static Error DocumentTypeRequired(int guestIndex) => Error.Problem(
    "OnlineCheckIn.DocumentTypeRequired",
    $"A document type is required for guest at index {guestIndex}.");

  public static Error DocumentNumberRequired(int guestIndex) => Error.Problem(
    "OnlineCheckIn.DocumentNumberRequired",
    $"A document number is required for guest at index {guestIndex}.");

  public static Error DocumentTypeNotAllowed(int guestIndex, DocumentType documentType, string nationalityAlpha2) =>
    Error.Problem(
      "OnlineCheckIn.DocumentTypeNotAllowed",
      $"Document type {documentType} is not allowed for nationality {nationalityAlpha2} (guest at index {guestIndex}).");

  public static Error VisaNumberRequired(int guestIndex) => Error.Problem(
    "OnlineCheckIn.VisaNumberRequired",
    $"A visa number is required for guest at index {guestIndex}.");

  public static Error BiometrikaNotAllowed(int guestIndex) => Error.Problem(
    "OnlineCheckIn.BiometrikaNotAllowed",
    $"BIOMETRIKA visa-exemption is not allowed for guest at index {guestIndex} (nationality does not qualify).");
}
