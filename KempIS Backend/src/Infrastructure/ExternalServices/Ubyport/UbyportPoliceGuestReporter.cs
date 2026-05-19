using System.Net;
using System.Text;
using System.Xml.Linq;
using Application.Abstractions.Reservations;
using Domain.Reservations.PoliceReports;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Infrastructure.ExternalServices.Ubyport;

internal sealed class UbyportPoliceGuestReporter(
  HttpClient httpClient,
  IOptions<UbyportOptions> options)
  : IPoliceGuestReporter
{
  public async Task<Result> SubmitAsync(
    IReadOnlyCollection<PoliceGuestEntry> entries,
    CancellationToken cancellationToken)
  {
    try
    {
      XDocument doc = UbyportXmlBuilder.BuildSoapEnvelope(options.Value, entries);
      string xml = doc.ToString(SaveOptions.DisableFormatting);

      using StringContent body = new(xml, Encoding.UTF8, "text/xml");
      body.Headers.Add("SOAPAction", $"\"{UbyportXmlBuilder.SoapActionValue}\"");

      using HttpResponseMessage response = await httpClient.PostAsync((Uri?)null, body, cancellationToken);

      if (response.StatusCode == HttpStatusCode.Unauthorized)
      {
        return Result.Failure(PoliceReportErrors.Unauthorized);
      }

      if (!response.IsSuccessStatusCode)
      {
        return Result.Failure(PoliceReportErrors.Rejected);
      }

      return Result.Success();
    }
    catch (HttpRequestException)
    {
      return Result.Failure(PoliceReportErrors.Unavailable);
    }
    catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
      return Result.Failure(PoliceReportErrors.Unavailable);
    }
  }
}
