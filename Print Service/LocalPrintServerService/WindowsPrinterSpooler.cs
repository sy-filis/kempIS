using System.Diagnostics;
using System.Drawing.Printing;
using System.Runtime.Versioning;
using System.Text;

namespace LocalPrintServerService;

[SupportedOSPlatform("windows")]
public sealed class WindowsPrinterSpooler(string sumatraPath) : IPrinterSpooler
{
  private static readonly TimeSpan PrintTimeout = TimeSpan.FromSeconds(30);

  public IReadOnlyList<string> GetInstalledPrinters()
  {
    var list = new List<string>();
    foreach (string name in PrinterSettings.InstalledPrinters)
    {
      list.Add(name);
    }
    return list;
  }

  public void PrintPdf(string printerName, byte[] pdfBytes)
  {
    if (!File.Exists(sumatraPath))
    {
      throw new PrinterSpoolerException(
          $"SumatraPDF executable not found at '{sumatraPath}'. " +
          "Place SumatraPDF.exe next to the service binary or set Sumatra:Path in appsettings.json.");
    }

    string tempPath = Path.Combine(Path.GetTempPath(), $"lps-{Guid.NewGuid():N}.pdf");
    try
    {
      File.WriteAllBytes(tempPath, pdfBytes);

      var psi = new ProcessStartInfo
      {
        FileName = sumatraPath,
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true
      };
      psi.ArgumentList.Add("-silent");
      psi.ArgumentList.Add("-print-to");
      psi.ArgumentList.Add(printerName);
      psi.ArgumentList.Add(tempPath);

      using Process process = Process.Start(psi)
          ?? throw new PrinterSpoolerException("Failed to start SumatraPDF process");

      // Drain both pipes on background threads. Synchronous ReadToEnd after
      // WaitForExit can deadlock if the child fills a pipe buffer before
      // exiting; async callbacks let the child write unimpeded.
      var stdout = new StringBuilder();
      var stderr = new StringBuilder();
      process.OutputDataReceived += (_, e) =>
      {
        if (e.Data is null)
        {
          return;
        }
        lock (stdout)
        {
          stdout.AppendLine(e.Data);
        }
      };
      process.ErrorDataReceived += (_, e) =>
      {
        if (e.Data is null)
        {
          return;
        }
        lock (stderr)
        {
          stderr.AppendLine(e.Data);
        }
      };
      process.BeginOutputReadLine();
      process.BeginErrorReadLine();

      if (!process.WaitForExit(PrintTimeout))
      {
        try
        { process.Kill(entireProcessTree: true); }
        catch { /* best effort */ }
        throw new PrinterSpoolerException(
            $"SumatraPDF did not exit within {PrintTimeout.TotalSeconds:0} seconds");
      }

      // Blocking overload flushes pending async-pipe callbacks (per docs).
      process.WaitForExit();

      if (process.ExitCode == 0)
      {
        return;
      }

      string detail;
      lock (stderr)
      {
        detail = stderr.ToString().Trim();
      }
      if (string.IsNullOrEmpty(detail))
      {
        lock (stdout)
        {
          detail = stdout.ToString().Trim();
        }
      }
      if (string.IsNullOrEmpty(detail))
      {
        detail = "(no output)";
      }
      throw new PrinterSpoolerException(
          $"SumatraPDF exited with code {process.ExitCode}: {detail}");
    }
    finally
    {
      try
      { File.Delete(tempPath); }
      catch { /* best effort */ }
    }
  }
}
