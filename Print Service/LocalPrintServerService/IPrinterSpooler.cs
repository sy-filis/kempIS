namespace LocalPrintServerService;

public interface IPrinterSpooler
{
  IReadOnlyList<string> GetInstalledPrinters();

  void PrintPdf(string printerName, byte[] pdfBytes);
}
