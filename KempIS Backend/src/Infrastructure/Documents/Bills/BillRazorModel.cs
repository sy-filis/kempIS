using Infrastructure.Documents.Bills.Resources;
using Microsoft.Extensions.Localization;

namespace Infrastructure.Documents.Bills;

public sealed record BillRazorModel(BillDocumentModel Bill, IStringLocalizer<BillResources> L);
