using Domain.Common;
using Domain.Finance.Bills;
using Domain.Finance.Invoices;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Reservations.Guests;

namespace Application.Retention;

internal static class RetentionAnonymizer
{
  internal const string AnonymizedString = "Anonymized";
  internal const string AnonymizedNumeric = "00000000";

  public static void Anonymize(Guest guest)
  {
    guest.FirstName = AnonymizedString;
    guest.LastName = AnonymizedString;
    guest.Address = AnonymizedAddress(guest.Address.CountryId);
    guest.DocumentType = null;
    guest.DocumentNumber = null;
    guest.VisaNumber = null;
    guest.Note = null;
    guest.SignaturePng = null;
    guest.SignatureCapturedAtUtc = null;
    guest.Scartation = null;
  }

  public static void Anonymize(Bill bill)
  {
    bill.Payer = AnonymizedPayer(bill.Payer.Address.CountryId);
    if (bill.LegalEntity is not null)
    {
      bill.LegalEntity = AnonymizedLegalEntity(bill.LegalEntity.Address.CountryId);
    }
    bill.DocumentContent = null;
    bill.DocumentGeneratedAtUtc = null;
    bill.Scartation = null;
  }

  public static void Anonymize(Invoice invoice)
  {
    if (invoice.Payer is not null)
    {
      invoice.Payer = AnonymizedPayer(invoice.Payer.Address.CountryId);
    }
    if (invoice.LegalEntity is not null)
    {
      invoice.LegalEntity = AnonymizedLegalEntity(invoice.LegalEntity.Address.CountryId);
    }
    invoice.Email = "anonymized@anonymized.invalid";
    invoice.PhoneNumber = AnonymizedNumeric;
    invoice.Scartation = null;
  }

  private static Address AnonymizedAddress(Guid countryId) =>
    new(countryId, AnonymizedString, AnonymizedString, AnonymizedString, AnonymizedString);

  private static Payer AnonymizedPayer(Guid countryId) => new()
  {
    Name = AnonymizedString,
    Surname = AnonymizedString,
    Address = AnonymizedAddress(countryId),
  };

  private static LegalEntity AnonymizedLegalEntity(Guid countryId) => new()
  {
    Name = AnonymizedString,
    Cin = AnonymizedNumeric,
    Tin = AnonymizedNumeric,
    Address = AnonymizedAddress(countryId),
  };
}
