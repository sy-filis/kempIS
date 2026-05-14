using System.Text.Json.Serialization;

namespace LocalPrintServerService;

public static class PrintersEndpoints
{
  public static IEndpointRouteBuilder MapPrintersEndpoints(this IEndpointRouteBuilder endpoints)
  {
    endpoints.MapGet("api/v1/printers", (IPrinterSpooler spooler) =>
            Results.Ok(spooler.GetInstalledPrinters()))
        .WithName("ListPrinters")
        .WithTags("Printers")
        .WithSummary("List all installed printers.")
        .Produces<string[]>(StatusCodes.Status200OK, "application/json")
        .WithResponseExample(StatusCodes.Status200OK, new[]
        {
                "Microsoft Print to PDF",
                "Microsoft XPS Document Writer",
                "OneNote (Desktop)"
        });

    endpoints.MapPost("api/v1/printers/{printerName}", async (
            string printerName,
            HttpRequest request,
            IPrinterSpooler spooler,
            ILoggerFactory loggerFactory) =>
        {
          ILogger logger = loggerFactory.CreateLogger("Printers");

          IReadOnlyList<string> installed = spooler.GetInstalledPrinters();
          if (!installed.Contains(printerName, StringComparer.OrdinalIgnoreCase))
          {
            logger.LogWarning("rejected: printer not found ({printer})", printerName);
            return Results.NotFound(new ErrorResponse("printer not found"));
          }

          // .Accepts<>(...) below restricts routing to application/pdf or
          // multipart/form-data; the framework returns 415 for anything else
          // before this handler runs, so we only branch between those two.
          string contentType = request.ContentType ?? string.Empty;
          byte[] pdfBytes;

          if (contentType.StartsWith("application/pdf", StringComparison.OrdinalIgnoreCase))
          {
            using var ms = new MemoryStream();
            await request.Body.CopyToAsync(ms);
            pdfBytes = ms.ToArray();
          }
          else
          {
            IFormCollection form = await request.ReadFormAsync();
            IFormFile? file = form.Files.FirstOrDefault();
            if (file is null)
            {
              logger.LogWarning("rejected: multipart request had no file part");
              return Results.BadRequest(new ErrorResponse("multipart request contained no file part"));
            }

            using var ms = new MemoryStream();
            await using (Stream stream = file.OpenReadStream())
            {
              await stream.CopyToAsync(ms);
            }

            pdfBytes = ms.ToArray();
          }

          if (!LooksLikePdf(pdfBytes))
          {
            logger.LogWarning("rejected: body is not a PDF ({bytes} bytes)", pdfBytes.Length);
            return Results.BadRequest(new ErrorResponse("body is not a PDF"));
          }

          try
          {
            spooler.PrintPdf(printerName, pdfBytes);
            logger.LogInformation("printed {bytes} bytes to {printer}", pdfBytes.Length, printerName);
            return Results.NoContent();
          }
          catch (PrinterSpoolerException ex)
          {
            logger.LogError(ex, "spooler call failed");
            return Results.Json(
                    new ErrorResponse("print failed", ex.Message),
                    statusCode: StatusCodes.Status500InternalServerError);
          }
        })
        .WithName("PrintPdf")
        .WithTags("Printers")
        .WithSummary("Submit a PDF to a named printer.")
        .WithDescription(
            "Upload a PDF to the named printer. The request body is either a raw `application/pdf` " +
            "(curl: `--data-binary @file.pdf -H 'Content-Type: application/pdf'`) or " +
            "`multipart/form-data` with the PDF as the first file part (as Swagger UI sends).")
        .Accepts<PdfUploadForm>("multipart/form-data", "application/pdf")
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
        .WithResponseExample(StatusCodes.Status400BadRequest, new ErrorResponse("body is not a PDF"))
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
        .WithResponseExample(StatusCodes.Status404NotFound, new ErrorResponse("printer not found"))
        .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError)
        .WithResponseExample(StatusCodes.Status500InternalServerError, new ErrorResponse("print failed", "SumatraPDF exited with code 2"));

    return endpoints;
  }

  // Reject obvious non-PDFs by magic bytes before invoking SumatraPDF.
  private static bool LooksLikePdf(ReadOnlySpan<byte> bytes) =>
      bytes.Length >= 5 &&
      bytes[0] == (byte)'%' && bytes[1] == (byte)'P' && bytes[2] == (byte)'D' &&
      bytes[3] == (byte)'F' && bytes[4] == (byte)'-';
}

public sealed record ErrorResponse(
    string Error,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Detail = null);

public sealed class PdfUploadForm
{
  public required IFormFile File { get; set; }
}
