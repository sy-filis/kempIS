using System.Globalization;
using System.Xml.Linq;
using Application.Abstractions.Reservations;

namespace Infrastructure.ExternalServices.Ubyport;

internal static class UbyportXmlBuilder
{
  private static readonly XNamespace SoapNs = "http://schemas.xmlsoap.org/soap/envelope/";
  private static readonly XNamespace AddressingNoneNs = "http://schemas.microsoft.com/ws/2005/05/addressing/none";
  private static readonly XNamespace MethodNs = "http://UBY.pcr.cz/WS_UBY";
  private static readonly XNamespace DataContractNs = "http://schemas.datacontract.org/2004/07/WS_UBY";
  private static readonly XNamespace XsiNs = "http://www.w3.org/2001/XMLSchema-instance";

  public const string SoapActionValue = "http://UBY.pcr.cz/WS_UBY/IWS_UBY/ZapisUbytovane";

  public static XElement BuildSeznam(UbyportOptions options, IReadOnlyCollection<PoliceGuestEntry> entries)
  {
    return BuildSeznamElement(options, entries);
  }

  public static XDocument BuildSoapEnvelope(UbyportOptions options, IReadOnlyCollection<PoliceGuestEntry> entries)
  {
    XElement envelope = new(SoapNs + "Envelope",
      new XAttribute(XNamespace.Xmlns + "s", SoapNs.NamespaceName),
      new XElement(SoapNs + "Header",
        new XElement(AddressingNoneNs + "Action",
          new XAttribute(SoapNs + "mustUnderstand", "1"),
          SoapActionValue)),
      new XElement(SoapNs + "Body",
        new XElement(MethodNs + "ZapisUbytovane",
          new XElement(MethodNs + "AutentificationCode", options.AuthenticationCode),
          BuildSeznamElement(options, entries))));

    return new XDocument(new XDeclaration("1.0", "utf-8", null), envelope);
  }

  private static XElement BuildSeznamElement(UbyportOptions options, IReadOnlyCollection<PoliceGuestEntry> entries)
  {
    return new XElement(DataContractNs + "Seznam",
      new XAttribute(XNamespace.Xmlns + "d4p1", DataContractNs.NamespaceName),
      new XAttribute(XNamespace.Xmlns + "i", XsiNs.NamespaceName),
      new XElement(DataContractNs + "Ubytovani",
        entries.Select(ToUbytovany)),
      new XElement(DataContractNs + "uCont", options.Contact),
      new XElement(DataContractNs + "uHomN", options.HouseNumber),
      new XElement(DataContractNs + "uIdub", options.IdUb.ToString(CultureInfo.InvariantCulture)),
      new XElement(DataContractNs + "uMark", options.Mark),
      TextOrNil(DataContractNs + "uName", options.Name),
      TextOrNil(DataContractNs + "uOb", options.Town),
      TextOrNil(DataContractNs + "uObCa", options.TownPart),
      TextOrNil(DataContractNs + "uOkr", options.District),
      TextOrNil(DataContractNs + "uOriN", options.OrientationNumber),
      new XElement(DataContractNs + "uPsc", options.Zip),
      TextOrNil(DataContractNs + "uStr", options.Street));
  }

  private static XElement ToUbytovany(PoliceGuestEntry e)
  {
    return new XElement(DataContractNs + "Ubytovany",
      new XElement(DataContractNs + "cDate",
        e.DateOfBirth.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)),
      new XElement(DataContractNs + "cDocN", e.DocumentNumberForCDocN),
      TextOrNil(DataContractNs + "cFirstN", e.FirstName),
      new XElement(DataContractNs + "cFrom",
        e.StayFrom.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)),
      new XElement(DataContractNs + "cNati", e.NationalityCode),
      TextOrNil(DataContractNs + "cNote", e.Note),
      Nil(DataContractNs + "cPlac"),
      TextOrNil(DataContractNs + "cPurp", e.PurposeOfStay),
      TextOrNil(DataContractNs + "cResi", e.PermanentAddressAbroad),
      Nil(DataContractNs + "cSpz"),
      TextOrNil(DataContractNs + "cSurN", e.LastName),
      new XElement(DataContractNs + "cUntil",
        e.StayUntil.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)),
      TextOrNil(DataContractNs + "cVisN", e.VisaNumber));
  }

  private static XElement TextOrNil(XName name, string? value)
    => string.IsNullOrEmpty(value)
      ? new XElement(name, new XAttribute(XsiNs + "nil", "true"))
      : new XElement(name, value);

  private static XElement Nil(XName name)
    => new XElement(name, new XAttribute(XsiNs + "nil", "true"));

}
