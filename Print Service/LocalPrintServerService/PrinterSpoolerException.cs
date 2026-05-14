namespace LocalPrintServerService;

public sealed class PrinterSpoolerException : Exception
{
  public PrinterSpoolerException(string message) : base(message) { }

  public PrinterSpoolerException(string message, Exception inner) : base(message, inner) { }
}
