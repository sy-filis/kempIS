using System.Globalization;
using Application.Abstractions.Data;
using Application.Abstractions.Documents;
using Application.Configuration;
using Application.Finance.Bills;
using Domain.Finance.BillItems;
using Domain.Finance.Bills;
using Domain.Finance.InvoiceItems;
using Domain.Finance.Invoices;
using Domain.Services.Services;
using Domain.Services.ServiceTexts;
using Infrastructure.Documents.Bills.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RazorLight;
using SharedKernel;

namespace Infrastructure.Documents.Bills;

internal sealed class RazorLightBillDocumentRenderer : IBillDocumentRenderer
{
  private const string TemplateKey = "Bills.Templates.Bill.cshtml";

  private readonly IApplicationDbContext _db;
  private readonly IPdfRenderer _pdfRenderer;
  private readonly IStringLocalizer<BillResources> _localizer;
  private readonly IOptions<CampSettings> _camp;
  private readonly ILogger<RazorLightBillDocumentRenderer> _logger;
  private readonly RazorLightEngine _engine;

  public RazorLightBillDocumentRenderer(
    IApplicationDbContext db,
    IPdfRenderer pdfRenderer,
    IStringLocalizer<BillResources> localizer,
    IOptions<CampSettings> camp,
    RazorLightEngine engine,
    ILogger<RazorLightBillDocumentRenderer> logger)
  {
    _db = db;
    _pdfRenderer = pdfRenderer;
    _localizer = localizer;
    _camp = camp;
    _engine = engine;
    _logger = logger;
  }

  public async Task<Result<BillDocumentRenderResult>> RenderAsync(Guid billId, CancellationToken cancellationToken)
  {
    Bill? bill = await _db.Bills
      .AsNoTracking()
      .FirstOrDefaultAsync(b => b.Id == billId, cancellationToken);

    if (bill is null)
    {
      return Result.Failure<BillDocumentRenderResult>(BillErrors.NotFound(billId));
    }

    string? languageCode = await _db.Languages
      .AsNoTracking()
      .Where(l => l.Id == bill.LanguageIdGuid)
      .Select(l => l.Code)
      .FirstOrDefaultAsync(cancellationToken);

    CultureInfo culture = ResolveCulture(languageCode);

    List<BillItem> items = await _db.BillItems
      .AsNoTracking()
      .Where(i => i.BillId == billId)
      .ToListAsync(cancellationToken);

    BillDocumentModel model = await MapModelAsync(bill, items, culture.Name, cancellationToken);

    string html;
    using (new CultureSwitch(culture))
    {
      BillRazorModel razorModel = new(model, _localizer);
      html = await _engine.CompileRenderAsync(TemplateKey, razorModel);
    }

    byte[] pdf = await _pdfRenderer.RenderAsync(html, cancellationToken);

    return Result.Success(new BillDocumentRenderResult(pdf, "application/pdf", culture.Name));
  }

  private CultureInfo ResolveCulture(string? languageCode)
  {
    if (string.IsNullOrWhiteSpace(languageCode))
    {
      _logger.LogWarning("Bill has no language code; falling back to invariant culture");
      return CultureInfo.InvariantCulture;
    }

    try
    {
      return CultureInfo.GetCultureInfo(languageCode);
    }
    catch (CultureNotFoundException ex)
    {
      _logger.LogWarning(ex, "Unknown culture '{Code}'; falling back to invariant culture", languageCode);
      return CultureInfo.InvariantCulture;
    }
  }

  private async Task<BillDocumentModel> MapModelAsync(
    Bill bill,
    List<BillItem> items,
    string languageCode,
    CancellationToken ct)
  {
    List<Invoice> linkedInvoices = await _db.Invoices
      .AsNoTracking()
      .Where(i => i.LinkedBillId == bill.Id)
      .ToListAsync(ct);

    List<InvoiceItem> allInvoiceItems = linkedInvoices.Count > 0
      ? await _db.InvoiceItems
        .AsNoTracking()
        .Where(ii => linkedInvoices.Select(i => i.Id).Contains(ii.InvoiceId))
        .ToListAsync(ct)
      : new List<InvoiceItem>();

    var serviceIds = items
      .Where(i => i.ServiceId.HasValue)
      .Select(i => i.ServiceId!.Value)
      .Distinct()
      .ToList();

    Dictionary<Guid, string> serviceNames = serviceIds.Count > 0
      ? await _db.Services
        .AsNoTracking()
        .Where(s => serviceIds.Contains(s.Id))
        .ToDictionaryAsync(s => s.Id, s => s.Name, ct)
      : new Dictionary<Guid, string>();

    Dictionary<Guid, string> serviceTexts = serviceIds.Count > 0
      ? await _db.ServiceTexts
        .AsNoTracking()
        .Where(st => serviceIds.Contains(st.ServiceId) && st.LanguageId == bill.LanguageIdGuid)
        .ToDictionaryAsync(st => st.ServiceId, st => st.PrintText, ct)
      : new Dictionary<Guid, string>();

    var vehicleRows = await _db.Vehicles
      .AsNoTracking()
      .Where(v => v.BillId == bill.Id && v.ServiceId != null)
      .Select(v => new { ServiceId = v.ServiceId!.Value, v.RegistrationNumber })
      .ToListAsync(ct);

    var vehiclesByService = vehicleRows
      .GroupBy(x => x.ServiceId)
      .ToDictionary(
        g => g.Key,
        g => g.Select(x => x.RegistrationNumber).OrderBy(s => s, StringComparer.Ordinal).ToList());

    var spotRows = await (
        from rsi in _db.ReservationSpotItems.AsNoTracking()
        join sg in _db.SpotGroups.AsNoTracking() on rsi.SpotGroupId equals sg.Id
        join sp in _db.Spots.AsNoTracking() on rsi.SpotId equals sp.Id
        where rsi.BillId == bill.Id && rsi.SpotId != null
        select new { sg.ServiceId, sp.Name })
      .ToListAsync(ct);

    var spotsByService = spotRows
      .GroupBy(x => x.ServiceId)
      .ToDictionary(
        g => g.Key,
        g => g.Select(x => x.Name).OrderBy(s => s, StringComparer.Ordinal).ToList());

    List<decimal> activeRates = await _db.VatRates
      .AsNoTracking()
      .Where(v => v.IsActive)
      .Select(v => v.Rate)
      .ToListAsync(ct);

    // UnitPrice is gross; billed quantity = recapSingle × recapDay (Quantity is a FE display field).
    var lines = items
      .Select(i => new BillDocumentLineModel(
        Description: ResolveDescription(i.ServiceId, serviceTexts, serviceNames),
        NetUnitPrice: Math.Round(
          i.UnitPrice / (1m + i.VatRatePercentage / 100m),
          2, MidpointRounding.AwayFromZero),
        UnitPrice: i.UnitPrice,
        RecapSingleQuantity: i.RecapSingleQuantity,
        RecapDayQuantity: i.RecapDayQuantity,
        VatRatePercentage: i.VatRatePercentage,
        LineTotal: Math.Round(
          (decimal)i.RecapSingleQuantity * i.RecapDayQuantity * i.UnitPrice, 2,
          MidpointRounding.AwayFromZero),
        VehiclesAndSpotsSuffix: BuildVehiclesAndSpotsSuffix(
          i.ServiceId, vehiclesByService, spotsByService)))
      .ToList();

    List<BillDocumentDeductionModel> deductionModels = new();

    foreach (Invoice invoice in linkedInvoices)
    {
      string invoiceNumber = invoice.Number ?? invoice.Id.ToString();

      var invoiceItems = allInvoiceItems
        .Where(ii => ii.InvoiceId == invoice.Id)
        .ToList();

      var ratesUsedHere = invoiceItems
        .Select(ii => decimal.Round(ii.VatRatePercentage, 2))
        .Distinct()
        .OrderBy(r => r)
        .ToList();

      var invoiceRecap = ratesUsedHere
        .Select(rate =>
        {
          decimal grossTotal = Math.Round(
            invoiceItems
              .Where(ii => decimal.Round(ii.VatRatePercentage, 2) == rate)
              .Sum(ii => Math.Round(ii.Quantity * ii.UnitPrice, 2, MidpointRounding.AwayFromZero)),
            2, MidpointRounding.AwayFromZero);
          decimal netTotal = Math.Round(grossTotal / (1m + rate / 100m), 2, MidpointRounding.AwayFromZero);
          decimal vatAmount = grossTotal - netTotal;
          return new BillDocumentVatRecapLineModel(
            VatRatePercentage: rate,
            NetTotal: netTotal,
            VatAmount: vatAmount,
            GrossTotal: grossTotal);
        })
        .ToList();

      decimal deducted = Math.Round(invoiceRecap.Sum(r => r.GrossTotal), 2, MidpointRounding.AwayFromZero);

      deductionModels.Add(new BillDocumentDeductionModel(
        InvoiceNumber: invoiceNumber,
        VatRecap: invoiceRecap,
        DeductedAmount: deducted));
    }

    decimal billItemsGross = lines.Sum(l => l.LineTotal);
    decimal deductionItemsGross = deductionModels.SelectMany(d => d.VatRecap).Sum(r => r.GrossTotal);
    decimal grossSubtotal = Math.Round(billItemsGross + deductionItemsGross, 2);

    // Round to 2 places so 12.0 and 12.00 collapse to one row.
    IEnumerable<decimal> usedRates = lines.Select(l => l.VatRatePercentage)
      .Concat(allInvoiceItems.Select(ii => ii.VatRatePercentage));

    var allRates = activeRates
      .Concat(usedRates)
      .Select(r => decimal.Round(r, 2))
      .Distinct()
      .OrderBy(r => r)
      .ToList();

    var vatRecap = allRates
      .Select(rate =>
      {
        decimal grossFromLines = Math.Round(
          lines.Where(l => decimal.Round(l.VatRatePercentage, 2) == rate).Sum(l => l.LineTotal),
          2, MidpointRounding.AwayFromZero);
        decimal grossFromInvoices = Math.Round(
          allInvoiceItems
            .Where(ii => decimal.Round(ii.VatRatePercentage, 2) == rate)
            .Sum(ii => Math.Round(ii.Quantity * ii.UnitPrice, 2, MidpointRounding.AwayFromZero)),
          2, MidpointRounding.AwayFromZero);

        decimal grossTotal = Math.Round(grossFromLines + grossFromInvoices, 2, MidpointRounding.AwayFromZero);
        decimal netTotal = Math.Round(grossTotal / (1m + rate / 100m), 2, MidpointRounding.AwayFromZero);
        decimal vatAmount = grossTotal - netTotal;
        return new BillDocumentVatRecapLineModel(
          VatRatePercentage: rate,
          NetTotal: netTotal,
          VatAmount: vatAmount,
          GrossTotal: grossTotal);
      })
      .ToList();

    string payerName = $"{bill.Payer.Name} {bill.Payer.Surname}".Trim();

    BillDocumentPartyModel payerModel = new(
      Name: payerName,
      Street: bill.Payer.Address.Street,
      City: bill.Payer.Address.City,
      PostalCode: bill.Payer.Address.ZipCode,
      Country: null);

    BillDocumentLegalEntityModel? legal = null;
    if (bill.LegalEntity is { } billLegal)
    {
      BillDocumentPartyModel legalAddress = new(
        Name: billLegal.Name,
        Street: billLegal.Address.Street,
        City: billLegal.Address.City,
        PostalCode: billLegal.Address.ZipCode,
        Country: null);

      legal = new BillDocumentLegalEntityModel(
        Name: billLegal.Name,
        Cin: billLegal.Cin,
        Tin: billLegal.Tin,
        Address: legalAddress);
    }

    CampSettings c = _camp.Value;
    string cityZip = string.IsNullOrWhiteSpace(c.ZipCode) && string.IsNullOrWhiteSpace(c.City)
      ? string.Empty
      : $"{c.ZipCode} {c.City}".Trim();

    CampLegalInfo camp = new(
      Name: c.Name,
      Street: c.Street,
      CityZip: cityZip,
      Cin: c.Cin,
      Tin: c.Tin,
      Phone: c.Phone,
      Email: c.Email,
      Web: c.Web);

    return new BillDocumentModel(
      Number: bill.Number,
      IssuedAtUtc: bill.IssuedAtUtc,
      LanguageCode: languageCode,
      IsRepair: bill.Kind == BillKind.Repair,
      RepairReason: bill.RepairReason,
      Camp: camp,
      Payer: payerModel,
      LegalEntity: legal,
      Lines: lines,
      Total: bill.Payment.Amount,
      PaymentType: bill.Payment.PaymentType.ToString(),
      Deductions: deductionModels,
      VatRecap: vatRecap,
      GrossSubtotal: grossSubtotal);
  }

  private static string ResolveDescription(
    Guid? serviceId,
    Dictionary<Guid, string> serviceTexts,
    Dictionary<Guid, string> serviceNames)
  {
    if (serviceId is not Guid id)
    {
      return string.Empty;
    }

    if (serviceTexts.TryGetValue(id, out string? text))
    {
      return text;
    }

    if (serviceNames.TryGetValue(id, out string? name))
    {
      return name;
    }

    return string.Empty;
  }

  private static string? BuildVehiclesAndSpotsSuffix(
    Guid? serviceId,
    Dictionary<Guid, List<string>> vehiclesByService,
    Dictionary<Guid, List<string>> spotsByService)
  {
    if (serviceId is not Guid id)
    {
      return null;
    }

    var names = new List<string>();
    if (vehiclesByService.TryGetValue(id, out List<string>? plates) && plates.Count > 0)
    {
      names.AddRange(plates);
    }

    if (spotsByService.TryGetValue(id, out List<string>? spots) && spots.Count > 0)
    {
      names.AddRange(spots);
    }

    return names.Count == 0 ? null : $"({string.Join(", ", names)})";
  }
}
