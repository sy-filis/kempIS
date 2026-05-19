using Infrastructure.Documents.Pdf;
using Microsoft.Playwright;
using Shouldly;
using Xunit;

namespace Infrastructure.UnitTests.Documents.Pdf;

[Trait("Category", "Playwright")]
public sealed class PlaywrightPdfRendererSmokeTests
{
  [Fact]
  public async Task RenderAsync_ProducesPdfBytes()
  {
    // Ensure Chromium is installed locally; no-op if already cached.
    int installExitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
    installExitCode.ShouldBe(0);

    IPlaywright playwright = await Playwright.CreateAsync();
    IBrowser browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    await using PlaywrightPdfRenderer renderer = new(browser, playwright);

    byte[] pdf = await renderer.RenderAsync("<html><body><h1>hello</h1></body></html>", default);

    pdf.Length.ShouldBeGreaterThan(1024);
    System.Text.Encoding.ASCII.GetString(pdf, 0, 5).ShouldBe("%PDF-");
  }
}
