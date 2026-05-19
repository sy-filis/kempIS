using System.Xml.Linq;
using Application.Abstractions.Reservations;
using Infrastructure.ExternalServices.Ubyport;

namespace Application.UnitTests.Infrastructure.Ubyport;

public sealed class UbyportXmlBuilderTests
{
  private static readonly XNamespace SoapNs = "http://schemas.xmlsoap.org/soap/envelope/";
  private static readonly XNamespace AddressingNoneNs = "http://schemas.microsoft.com/ws/2005/05/addressing/none";
  private static readonly XNamespace MethodNs = "http://UBY.pcr.cz/WS_UBY";
  private static readonly XNamespace DcNs = "http://schemas.datacontract.org/2004/07/WS_UBY";
  private static readonly XNamespace XsiNs = "http://www.w3.org/2001/XMLSchema-instance";

  private static UbyportOptions Options() => new()
  {
    EndpointUrl = "https://ubyport.pcr.cz/ws_uby_test/ws_uby.svc",
    Username = "u",
    Password = "p",
    AuthenticationCode = "code",
    IdUb = 125,
    Mark = "aaa",
    Name = "U Dubu",
    Contact = "test@seznam.cz",
    District = "Praha",
    Town = "Praha",
    TownPart = "Nusle",
    Street = "Lennonova",
    HouseNumber = "123",
    OrientationNumber = "152",
    Zip = "1110"
  };

  private static PoliceGuestEntry Entry(
    string? note = null,
    string? purpose = null,
    string docN = "aa123",
    string nationality = "UK")
    => new(
      GuestId: Guid.NewGuid(),
      StayFrom: new DateTime(2019, 6, 25, 15, 0, 0, DateTimeKind.Utc),
      StayUntil: new DateTime(2019, 6, 25, 10, 0, 0, DateTimeKind.Unspecified),
      LastName: "Abbas",
      FirstName: "Hassan",
      DateOfBirth: new DateOnly(1950, 1, 1),
      NationalityCode: nationality,
      DocumentNumberForCDocN: docN,
      VisaNumber: "123456",
      PermanentAddressAbroad: "Uzgorod",
      Note: note,
      PurposeOfStay: purpose);

  [Fact]
  public void BuildSeznam_RootIsSeznamInDataContractNamespace()
  {
    XElement root = UbyportXmlBuilder.BuildSeznam(Options(), new[] { Entry() });

    root.Name.ShouldBe(DcNs + "Seznam");
  }

  [Fact]
  public void BuildSeznam_EmitsAccommodationHeaderFromOptions()
  {
    XElement root = UbyportXmlBuilder.BuildSeznam(Options(), new[] { Entry() });

    root.Element(DcNs + "uIdub")!.Value.ShouldBe("125");
    root.Element(DcNs + "uMark")!.Value.ShouldBe("aaa");
    root.Element(DcNs + "uName")!.Value.ShouldBe("U Dubu");
    root.Element(DcNs + "uCont")!.Value.ShouldBe("test@seznam.cz");
    root.Element(DcNs + "uOkr")!.Value.ShouldBe("Praha");
    root.Element(DcNs + "uOb")!.Value.ShouldBe("Praha");
    root.Element(DcNs + "uObCa")!.Value.ShouldBe("Nusle");
    root.Element(DcNs + "uStr")!.Value.ShouldBe("Lennonova");
    root.Element(DcNs + "uHomN")!.Value.ShouldBe("123");
    root.Element(DcNs + "uOriN")!.Value.ShouldBe("152");
    root.Element(DcNs + "uPsc")!.Value.ShouldBe("1110");
  }

  [Fact]
  public void BuildSeznam_EmitsOneUbytovanyElementPerEntry()
  {
    XElement root = UbyportXmlBuilder.BuildSeznam(Options(), new[] { Entry(), Entry() });
    XElement list = root.Element(DcNs + "Ubytovani")!;
    list.Elements(DcNs + "Ubytovany").Count().ShouldBe(2);
  }

  [Fact]
  public void BuildSeznam_FormatsStayDatesAsISOWithoutZone()
  {
    XElement root = UbyportXmlBuilder.BuildSeznam(Options(), new[] { Entry() });
    XElement ub = root.Element(DcNs + "Ubytovani")!.Element(DcNs + "Ubytovany")!;
    ub.Element(DcNs + "cFrom")!.Value.ShouldBe("2019-06-25T15:00:00");
    ub.Element(DcNs + "cUntil")!.Value.ShouldBe("2019-06-25T10:00:00");
  }

  [Fact]
  public void BuildSeznam_FormatsDateOfBirthAsDDDotMMDotYYYY()
  {
    XElement root = UbyportXmlBuilder.BuildSeznam(Options(), new[] { Entry() });
    XElement ub = root.Element(DcNs + "Ubytovani")!.Element(DcNs + "Ubytovany")!;
    ub.Element(DcNs + "cDate")!.Value.ShouldBe("01.01.1950");
  }

  [Fact]
  public void BuildSeznam_EmitsNoneForCDocNWhenProvided()
  {
    XElement root = UbyportXmlBuilder.BuildSeznam(Options(), new[] { Entry(docN: "NONE") });
    XElement ub = root.Element(DcNs + "Ubytovani")!.Element(DcNs + "Ubytovany")!;
    ub.Element(DcNs + "cDocN")!.Value.ShouldBe("NONE");
  }

  [Fact]
  public void BuildSeznam_EmitsCNoteElementWhenNoteIsSet()
  {
    XElement root = UbyportXmlBuilder.BuildSeznam(Options(), new[] { Entry(note: "POBYT X") });
    XElement ub = root.Element(DcNs + "Ubytovani")!.Element(DcNs + "Ubytovany")!;
    XElement cNote = ub.Element(DcNs + "cNote")!;
    cNote.Value.ShouldBe("POBYT X");
    cNote.Attribute(XsiNs + "nil").ShouldBeNull();
  }

  [Fact]
  public void BuildSeznam_EmitsCNoteAsNilWhenNoteIsNull()
  {
    XElement root = UbyportXmlBuilder.BuildSeznam(Options(), new[] { Entry(note: null) });
    XElement ub = root.Element(DcNs + "Ubytovani")!.Element(DcNs + "Ubytovany")!;
    ub.Element(DcNs + "cNote")!.Attribute(XsiNs + "nil")?.Value.ShouldBe("true");
  }

  [Fact]
  public void BuildSeznam_EmitsCPurpAsNilWhenPurposeIsNull()
  {
    XElement root = UbyportXmlBuilder.BuildSeznam(Options(), new[] { Entry(purpose: null) });
    XElement ub = root.Element(DcNs + "Ubytovani")!.Element(DcNs + "Ubytovany")!;
    ub.Element(DcNs + "cPurp")!.Attribute(XsiNs + "nil")?.Value.ShouldBe("true");
  }

  [Fact]
  public void BuildSoapEnvelope_HasAddressingActionHeaderAndBodyStructure()
  {
    UbyportOptions opts = Options();
    opts.AuthenticationCode = "SECRET-123";

    XDocument doc = UbyportXmlBuilder.BuildSoapEnvelope(opts, new[] { Entry() });

    doc.Root!.Name.ShouldBe(SoapNs + "Envelope");

    XElement header = doc.Root.Element(SoapNs + "Header")!;
    XElement action = header.Element(AddressingNoneNs + "Action")!;
    action.Value.ShouldBe("http://UBY.pcr.cz/WS_UBY/IWS_UBY/ZapisUbytovane");
    action.Attribute(SoapNs + "mustUnderstand")!.Value.ShouldBe("1");

    XElement body = doc.Root.Element(SoapNs + "Body")!;
    XElement method = body.Element(MethodNs + "ZapisUbytovane")!;
    method.ShouldNotBeNull();

    var children = method.Elements().ToList();
    children.Count.ShouldBe(2);
    children[0].Name.ShouldBe(MethodNs + "AutentificationCode");
    children[0].Value.ShouldBe("SECRET-123");
    children[1].Name.ShouldBe(DcNs + "Seznam");

    XElement seznam = children[1];
    seznam.Element(DcNs + "Ubytovani")!.Elements(DcNs + "Ubytovany").Count().ShouldBe(1);
  }

  [Fact]
  public void BuildSeznam_OptionalAccommodationFieldsEmitNilWhenNull()
  {
    UbyportOptions opts = Options();
    opts.TownPart = null;
    opts.OrientationNumber = null;

    XElement seznam = UbyportXmlBuilder.BuildSeznam(opts, new[] { Entry() });

    seznam.Element(DcNs + "uObCa")!.Attribute(XsiNs + "nil")!.Value.ShouldBe("true");
    seznam.Element(DcNs + "uOriN")!.Attribute(XsiNs + "nil")!.Value.ShouldBe("true");
  }

  [Fact]
  public void BuildSeznam_CPlacAndCSpzAlwaysEmitNil()
  {
    XElement seznam = UbyportXmlBuilder.BuildSeznam(Options(), new[] { Entry() });
    XElement ubytovany = seznam.Element(DcNs + "Ubytovani")!.Element(DcNs + "Ubytovany")!;

    ubytovany.Element(DcNs + "cPlac")!.Attribute(XsiNs + "nil")!.Value.ShouldBe("true");
    ubytovany.Element(DcNs + "cSpz")!.Attribute(XsiNs + "nil")!.Value.ShouldBe("true");
  }
}
