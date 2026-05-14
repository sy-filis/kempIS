using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LocalPrintServerService.Tests;

public sealed class PrintEndpointTests
{
  private static readonly byte[] MinimalPdf =
      Encoding.ASCII.GetBytes("%PDF-1.4\n%EOF\n");

  private static WebApplicationFactory<Program> CreateFactory(FakePrinterSpooler fake) =>
      new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
      {
        builder.ConfigureServices(services =>
          {
            services.RemoveAll<IPrinterSpooler>();
            services.AddSingleton<IPrinterSpooler>(fake);
          });
      });

  [Fact]
  public async Task Post_raw_pdf_returns_204_and_forwards_bytes()
  {
    var fake = new FakePrinterSpooler();
    fake.InstalledPrinters.Add("Xerox");

    await using WebApplicationFactory<Program> factory = CreateFactory(fake);
    using HttpClient client = factory.CreateClient();

    using var content = new ByteArrayContent(MinimalPdf);
    content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

    HttpResponseMessage response = await client.PostAsync("/api/v1/printers/Xerox", content);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

    FakePrinterSpooler.PrintCall call = Assert.Single(fake.PrintCalls);
    Assert.Equal("Xerox", call.PrinterName);
    Assert.Equal(MinimalPdf, call.PdfBytes);
  }

  [Fact]
  public async Task Post_multipart_returns_204_and_forwards_bytes()
  {
    var fake = new FakePrinterSpooler();
    fake.InstalledPrinters.Add("Xerox");

    await using WebApplicationFactory<Program> factory = CreateFactory(fake);
    using HttpClient client = factory.CreateClient();

    using var multipart = new MultipartFormDataContent();
    var fileContent = new ByteArrayContent(MinimalPdf);
    fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
    multipart.Add(fileContent, "file", "invoice.pdf");

    HttpResponseMessage response = await client.PostAsync("/api/v1/printers/Xerox", multipart);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

    FakePrinterSpooler.PrintCall call = Assert.Single(fake.PrintCalls);
    Assert.Equal(MinimalPdf, call.PdfBytes);
  }

  [Fact]
  public async Task Post_multipart_with_no_file_part_returns_400()
  {
    var fake = new FakePrinterSpooler();
    fake.InstalledPrinters.Add("Xerox");

    await using WebApplicationFactory<Program> factory = CreateFactory(fake);
    using HttpClient client = factory.CreateClient();

    using var multipart = new MultipartFormDataContent();
    multipart.Add(new StringContent("hello"), "note");

    HttpResponseMessage response = await client.PostAsync("/api/v1/printers/Xerox", multipart);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Empty(fake.PrintCalls);
  }

  [Fact]
  public async Task Post_to_unknown_printer_returns_404_with_printer_not_found()
  {
    var fake = new FakePrinterSpooler();
    fake.InstalledPrinters.Add("Xerox");

    await using WebApplicationFactory<Program> factory = CreateFactory(fake);
    using HttpClient client = factory.CreateClient();

    using var content = new ByteArrayContent(MinimalPdf);
    content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

    HttpResponseMessage response = await client.PostAsync("/api/v1/printers/DoesNotExist", content);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    string body = await response.Content.ReadAsStringAsync();
    Assert.Contains("printer not found", body);
    Assert.Empty(fake.PrintCalls);
  }

  [Fact]
  public async Task Post_with_unsupported_content_type_returns_415()
  {
    var fake = new FakePrinterSpooler();
    fake.InstalledPrinters.Add("Xerox");

    await using WebApplicationFactory<Program> factory = CreateFactory(fake);
    using HttpClient client = factory.CreateClient();

    using var content = new StringContent("{}", Encoding.UTF8, "application/json");

    HttpResponseMessage response = await client.PostAsync("/api/v1/printers/Xerox", content);

    // The `.Accepts<PdfUploadForm>("multipart/form-data", "application/pdf")` metadata
    // on the endpoint makes ASP.NET Core reject other content types at 415 before the
    // handler runs.
    Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    Assert.Empty(fake.PrintCalls);
  }

  [Fact]
  public async Task Post_with_non_pdf_body_returns_400()
  {
    var fake = new FakePrinterSpooler();
    fake.InstalledPrinters.Add("Xerox");

    await using WebApplicationFactory<Program> factory = CreateFactory(fake);
    using HttpClient client = factory.CreateClient();

    using var content = new ByteArrayContent(Encoding.ASCII.GetBytes("not a pdf"));
    content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

    HttpResponseMessage response = await client.PostAsync("/api/v1/printers/Xerox", content);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    string body = await response.Content.ReadAsStringAsync();
    Assert.Contains("body is not a PDF", body);
    Assert.Empty(fake.PrintCalls);
  }

  public sealed record ErrorResponse(string Error, string Detail);

  [Fact]
  public async Task Post_when_spooler_throws_returns_500_with_error_detail()
  {
    var fake = new FakePrinterSpooler();
    fake.InstalledPrinters.Add("Xerox");
    fake.PrintPdfHandler = (_, _) =>
        throw new PrinterSpoolerException("SumatraPDF exited with code 2");

    await using WebApplicationFactory<Program> factory = CreateFactory(fake);
    using HttpClient client = factory.CreateClient();

    using var content = new ByteArrayContent(MinimalPdf);
    content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

    HttpResponseMessage response = await client.PostAsync("/api/v1/printers/Xerox", content);

    Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    ErrorResponse? body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
    Assert.NotNull(body);
    Assert.Contains("SumatraPDF exited with code 2", body!.Detail);
    Assert.Equal("print failed", body.Error);
  }
}
