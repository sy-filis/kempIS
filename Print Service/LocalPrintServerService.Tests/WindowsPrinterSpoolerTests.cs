using System.Runtime.Versioning;

namespace LocalPrintServerService.Tests;

[SupportedOSPlatform("windows")]
public sealed class WindowsPrinterSpoolerTests
{
  [Fact(Skip = "Hits the Windows printer enumeration API. Remove Skip to run locally.")]
  public void GetInstalledPrinters_returns_non_null_list()
  {
    var spooler = new WindowsPrinterSpooler(sumatraPath: "unused-for-enumeration.exe");

    IReadOnlyList<string> printers = spooler.GetInstalledPrinters();

    Assert.NotNull(printers);
  }

  [Fact(Skip = "Spawns SumatraPDF with a non-PDF payload and asserts a non-zero exit is surfaced. Remove Skip to run locally; requires SumatraPDF.exe beside the test binary.")]
  public void PrintPdf_surfaces_sumatra_nonzero_exit_as_spooler_exception()
  {
    string sumatra = Path.Combine(AppContext.BaseDirectory, "SumatraPDF.exe");
    var spooler = new WindowsPrinterSpooler(sumatra);
    string anyPrinter = spooler.GetInstalledPrinters().First();

    PrinterSpoolerException ex = Assert.Throws<PrinterSpoolerException>(
        () => spooler.PrintPdf(anyPrinter, new byte[] { 0x00, 0x01, 0x02 }));

    Assert.Contains("SumatraPDF", ex.Message);
  }
}
