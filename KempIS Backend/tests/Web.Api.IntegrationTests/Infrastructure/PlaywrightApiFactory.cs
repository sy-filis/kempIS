namespace Web.Api.IntegrationTests.Infrastructure;

public sealed class PlaywrightApiFactory : ApiFactory
{
  protected override bool SubstituteFinancialClosingRenderer => false;
}
