using Application.Abstractions.Documents;
using Microsoft.Playwright;

namespace Infrastructure.Documents.Pdf;

internal sealed class PlaywrightPdfRenderer : IPdfRenderer, IAsyncDisposable
{
  private readonly IBrowser _browser;
  private readonly IPlaywright _playwright;

  public PlaywrightPdfRenderer(IBrowser browser, IPlaywright playwright)
  {
    _browser = browser;
    _playwright = playwright;
  }

  public Task<byte[]> RenderAsync(string html, CancellationToken cancellationToken) =>
    RenderAsync(html, PdfPageOptions.A4Portrait, cancellationToken);

  public async Task<byte[]> RenderAsync(string html, PdfPageOptions options, CancellationToken cancellationToken)
  {
    _ = cancellationToken;  // Playwright APIs don't accept a CT

    IBrowserContext context = await _browser.NewContextAsync();
    try
    {
      IPage page = await context.NewPageAsync();
      await page.SetContentAsync(html, new PageSetContentOptions { WaitUntil = WaitUntilState.NetworkIdle });

      PagePdfOptions pdfOptions = new()
      {
        PrintBackground = options.PrintBackground,
        Landscape = options.Landscape,
        Margin = new Margin
        {
          Top = options.MarginTop,
          Bottom = options.MarginBottom,
          Left = options.MarginLeft,
          Right = options.MarginRight
        }
      };

      if (options.Width is not null && options.Height is not null)
      {
        pdfOptions.Width = options.Width;
        pdfOptions.Height = options.Height;
      }
      else
      {
        pdfOptions.Format = options.Format;
      }

      return await page.PdfAsync(pdfOptions);
    }
    finally
    {
      await context.CloseAsync();
    }
  }

  public async ValueTask DisposeAsync()
  {
    await _browser.DisposeAsync();
    _playwright.Dispose();
  }
}
