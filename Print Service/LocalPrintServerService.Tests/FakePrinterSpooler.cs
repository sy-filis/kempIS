using LocalPrintServerService;

namespace LocalPrintServerService.Tests;

public sealed class FakePrinterSpooler : IPrinterSpooler
{
  public List<string> InstalledPrinters { get; } = new();

  public List<PrintCall> PrintCalls { get; } = new();

  public Action<string, byte[]>? PrintPdfHandler { get; set; }

  public IReadOnlyList<string> GetInstalledPrinters() => InstalledPrinters;

  public void PrintPdf(string printerName, byte[] pdfBytes)
  {
    PrintCalls.Add(new PrintCall(printerName, pdfBytes));
    PrintPdfHandler?.Invoke(printerName, pdfBytes);
  }

  public sealed record PrintCall(string PrinterName, byte[] PdfBytes);
}
