using System.Globalization;

namespace Infrastructure.Documents;

internal sealed class CultureSwitch : IDisposable
{
  private readonly CultureInfo _previousCulture;
  private readonly CultureInfo _previousUiCulture;

  public CultureSwitch(CultureInfo culture)
  {
    _previousCulture = CultureInfo.CurrentCulture;
    _previousUiCulture = CultureInfo.CurrentUICulture;
    CultureInfo.CurrentCulture = culture;
    CultureInfo.CurrentUICulture = culture;
  }

  public void Dispose()
  {
    CultureInfo.CurrentCulture = _previousCulture;
    CultureInfo.CurrentUICulture = _previousUiCulture;
  }
}
