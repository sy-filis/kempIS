using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Documents;
using Application.Configuration;
using Application.Finance.FinancialClosings;
using Domain.Finance.Bills;
using Domain.Finance.FinancialClosings;
using Domain.Finance.Payments;
using Domain.Services.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RazorLight;
using SharedKernel;

namespace Infrastructure.Documents.FinancialClosings;

internal sealed class RazorLightFinancialClosingReportRenderer : IFinancialClosingReportRenderer
{
  private sealed record ItemRow(
    Guid BillId,
    string BillNumber,
    bool IsStorno,
    PaymentType PaymentType,
    string ServiceTypeName,
    ServiceGroup ServiceGroup,
    decimal VatRatePercentage,
    uint RecapSingleQuantity,
    uint RecapDayQuantity,
    decimal UnitPrice);

  private const string TemplateKey = "FinancialClosings.Templates.FinancialClosingReport.cshtml";

  private readonly IApplicationDbContext _db;
  private readonly IIdentityService _identity;
  private readonly IOptions<CampSettings> _camp;
  private readonly IPdfRenderer _pdfRenderer;
  private readonly RazorLightEngine _engine;
  private readonly ILogger<RazorLightFinancialClosingReportRenderer> _logger;

  public RazorLightFinancialClosingReportRenderer(
    IApplicationDbContext db,
    IIdentityService identity,
    IOptions<CampSettings> camp,
    IPdfRenderer pdfRenderer,
    RazorLightEngine engine,
    ILogger<RazorLightFinancialClosingReportRenderer> logger)
  {
    _db = db;
    _identity = identity;
    _camp = camp;
    _pdfRenderer = pdfRenderer;
    _engine = engine;
    _logger = logger;
  }

  public async Task<Result<byte[]>> RenderAsync(FinancialClosing closing, CancellationToken cancellationToken)
  {
    if (_logger.IsEnabled(LogLevel.Debug))
    {
      _logger.LogDebug("Rendering closing report for {ClosingId}", closing.Id);
    }

    // BillItems with ServiceId == null are dropped by the inner join to Services.
    List<ItemRow> itemRows = await (
      from bi in _db.BillItems.AsNoTracking()
      join b in _db.Bills.AsNoTracking() on bi.BillId equals b.Id
      join s in _db.Services.AsNoTracking() on bi.ServiceId equals s.Id
      join st in _db.ServiceTypes.AsNoTracking() on s.ServiceTypeId equals st.Id
      where b.FinancialClosingId == closing.Id
      orderby b.IssuedAtUtc, b.Number, bi.Id
      select new ItemRow(
        b.Id,
        b.Number,
        b.Kind == BillKind.Repair,
        b.Payment.PaymentType,
        st.Name,
        s.ServiceGroup,
        bi.VatRatePercentage,
        bi.RecapSingleQuantity,
        bi.RecapDayQuantity,
        bi.UnitPrice))
      .ToListAsync(cancellationToken);

    List<string> activeServiceTypeNames = await _db.ServiceTypes
      .AsNoTracking()
      .Where(t => t.IsActive)
      .Select(t => t.Name)
      .ToListAsync(cancellationToken);

    List<decimal> activeVatRates = await _db.VatRates
      .AsNoTracking()
      .Where(v => v.IsActive)
      .Select(v => v.Rate)
      .ToListAsync(cancellationToken);

    List<decimal> historicalVatRates = await _db.BillItems
      .AsNoTracking()
      .Select(bi => bi.VatRatePercentage)
      .Distinct()
      .ToListAsync(cancellationToken);

    string cashierLabel = await ResolveCashierLabelAsync(closing.CreatedByUserId, cancellationToken);
    CampSettings camp = _camp.Value;

    FinancialClosingReportModel model = BuildModel(
      closing, itemRows, cashierLabel, camp,
      activeServiceTypeNames, activeVatRates, historicalVatRates);

    string html = await _engine.CompileRenderAsync(TemplateKey, model);

    byte[] pdf = await _pdfRenderer.RenderAsync(html, PdfPageOptions.A4Landscape, cancellationToken);

    return Result.Success(pdf);
  }

  private async Task<string> ResolveCashierLabelAsync(Guid? userId, CancellationToken ct)
  {
    if (userId is null)
    {
      return string.Empty;
    }

    Result<UserDetail> result = await _identity.GetUserAsync(userId.Value, ct);
    return result.IsSuccess ? result.Value.Name : string.Empty;
  }

  private static FinancialClosingReportModel BuildModel(
    FinancialClosing closing,
    IReadOnlyList<ItemRow> items,
    string cashierLabel,
    CampSettings camp,
    IReadOnlyList<string> activeServiceTypeNames,
    IReadOnlyList<decimal> activeVatRates,
    IReadOnlyList<decimal> historicalVatRates)
  {
    var serviceTypeColumns = items
      .Select(r => r.ServiceTypeName)
      .Concat(activeServiceTypeNames)
      .Distinct(StringComparer.Ordinal)
      .OrderBy(n => n, StringComparer.Ordinal)
      .ToList();

    // Round to collapse 12.0 and 12.00 into one column.
    var vatRates = items
      .Select(r => r.VatRatePercentage)
      .Concat(activeVatRates)
      .Concat(historicalVatRates)
      .Select(r => decimal.Round(r, 2))
      .Distinct()
      .OrderByDescending(r => r)
      .ToList();

    List<PaymentSection> sections = BuildSections(items);

    ReportFooter footer = BuildFooter(items);

    string campCityZip = string.Join(' ', new[] { camp.ZipCode, camp.City }
      .Where(s => !string.IsNullOrWhiteSpace(s)));

    return new FinancialClosingReportModel(
      SequentialNumber: closing.FinancialClosingId,
      ClosedAtUtc: closing.ClosedAtUtc,
      CashierLabel: cashierLabel,
      CampName: camp.Name,
      CampStreet: camp.Street,
      CampCityZip: campCityZip,
      ServiceTypeColumns: serviceTypeColumns,
      VatRates: vatRates,
      Sections: sections,
      Footer: footer);
  }

  private static List<PaymentSection> BuildSections(IReadOnlyList<ItemRow> items)
  {
    var sections = new List<PaymentSection>();

    foreach (PaymentType paymentType in new[] { PaymentType.Card, PaymentType.Cash })
    {
      var sectionItems = items.Where(r => r.PaymentType == paymentType).ToList();
      if (sectionItems.Count == 0)
      {
        continue;
      }

      var bills = sectionItems
        .GroupBy(r => r.BillId)
        .Select(g => BuildBillRow(g.First().BillNumber, g.First().IsStorno, g.ToList()))
        .ToList();

      BillRow subtotal = AggregateRows(bills, label: string.Empty);
      BillRow documentTotal = subtotal;

      sections.Add(new PaymentSection(
        Title: paymentType == PaymentType.Card ? "Platby kartou" : "Platby hotově",
        Bills: bills,
        Subtotal: subtotal,
        DocumentTotal: documentTotal));
    }

    return sections;
  }

  private static BillRow BuildBillRow(string billNumber, bool isStorno, IReadOnlyList<ItemRow> items)
  {
    var serviceTypeAmounts = new Dictionary<string, decimal>();
    var vatBases = new Dictionary<decimal, decimal>();
    var vatAmounts = new Dictionary<decimal, decimal>();
    decimal total = 0m;

    foreach (ItemRow item in items)
    {
      decimal gross = GrossOf(item);
      total += gross;

      serviceTypeAmounts.TryGetValue(item.ServiceTypeName, out decimal soFar);
      serviceTypeAmounts[item.ServiceTypeName] = soFar + gross;

      if (item.ServiceGroup == ServiceGroup.RecreationFees)
      {
        continue;
      }

      decimal net = Math.Round(gross / (1m + item.VatRatePercentage / 100m), 2, MidpointRounding.AwayFromZero);
      decimal vat = gross - net;

      vatBases.TryGetValue(item.VatRatePercentage, out decimal baseSoFar);
      vatBases[item.VatRatePercentage] = baseSoFar + net;

      if (item.VatRatePercentage > 0m)
      {
        vatAmounts.TryGetValue(item.VatRatePercentage, out decimal vatSoFar);
        vatAmounts[item.VatRatePercentage] = vatSoFar + vat;
      }
    }

    return new BillRow(
      BillNumber: billNumber,
      IsStorno: isStorno,
      ServiceTypeAmounts: serviceTypeAmounts,
      Total: total,
      VatBases: vatBases,
      VatAmounts: vatAmounts);
  }

  private static BillRow AggregateRows(IReadOnlyList<BillRow> bills, string label)
  {
    var serviceTypeAmounts = new Dictionary<string, decimal>();
    var vatBases = new Dictionary<decimal, decimal>();
    var vatAmounts = new Dictionary<decimal, decimal>();
    decimal total = 0m;

    foreach (BillRow bill in bills)
    {
      total += bill.Total;

      foreach ((string key, decimal value) in bill.ServiceTypeAmounts)
      {
        serviceTypeAmounts.TryGetValue(key, out decimal soFar);
        serviceTypeAmounts[key] = soFar + value;
      }

      foreach ((decimal rate, decimal value) in bill.VatBases)
      {
        vatBases.TryGetValue(rate, out decimal soFar);
        vatBases[rate] = soFar + value;
      }

      foreach ((decimal rate, decimal value) in bill.VatAmounts)
      {
        vatAmounts.TryGetValue(rate, out decimal soFar);
        vatAmounts[rate] = soFar + value;
      }
    }

    return new BillRow(
      BillNumber: label,
      IsStorno: false,
      ServiceTypeAmounts: serviceTypeAmounts,
      Total: total,
      VatBases: vatBases,
      VatAmounts: vatAmounts);
  }

  // UnitPrice is gross; billed quantity = recapSingle × recapDay (Quantity is a FE display field).
  private static decimal GrossOf(ItemRow item) =>
    Math.Round((decimal)item.RecapSingleQuantity * item.RecapDayQuantity * item.UnitPrice,
      2, MidpointRounding.AwayFromZero);

  private static ReportFooter BuildFooter(IReadOnlyList<ItemRow> items)
  {
    decimal totalNet = 0m;
    decimal totalVat = 0m;
    decimal grandTotal = 0m;

    foreach (ItemRow item in items)
    {
      decimal gross = GrossOf(item);
      grandTotal += gross;

      if (item.ServiceGroup == ServiceGroup.RecreationFees)
      {
        continue;
      }

      decimal net = Math.Round(gross / (1m + item.VatRatePercentage / 100m), 2, MidpointRounding.AwayFromZero);
      decimal vat = gross - net;

      totalNet += net;
      totalVat += vat;
    }

    return new ReportFooter(
      TotalNet: totalNet,
      TotalVat: totalVat,
      GrandTotal: grandTotal);
  }
}
