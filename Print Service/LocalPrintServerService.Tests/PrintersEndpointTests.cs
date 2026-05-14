using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LocalPrintServerService.Tests;

public sealed class PrintersEndpointTests
{
  [Fact]
  public async Task Get_printers_returns_installed_printer_names()
  {
    var fake = new FakePrinterSpooler();
    fake.InstalledPrinters.Add("Xerox WorkCentre 7845");
    fake.InstalledPrinters.Add("Canon G5000 series");

    await using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
          builder.ConfigureServices(services =>
              {
                services.RemoveAll<IPrinterSpooler>();
                services.AddSingleton<IPrinterSpooler>(fake);
              });
        });

    using HttpClient client = factory.CreateClient();
    HttpResponseMessage response = await client.GetAsync("/api/v1/printers");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    string[]? names = await response.Content.ReadFromJsonAsync<string[]>();
    Assert.NotNull(names);
    Assert.Equal(new[] { "Xerox WorkCentre 7845", "Canon G5000 series" }, names);
  }
}
