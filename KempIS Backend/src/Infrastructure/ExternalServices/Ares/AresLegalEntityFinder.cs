using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Abstractions.Finance;
using Application.Finance.LegalEntities.Queries.GetLegalEntityFromAres;
using Domain.Finance.LegalEntities;
using SharedKernel;

namespace Infrastructure.ExternalServices.Ares;

internal sealed class AresLegalEntityFinder(HttpClient httpClient) : ILegalEntityFinder
{
  public async Task<Result<LegalEntityFinderResponse>> FindByCinAsync(
    string cin,
    CancellationToken cancellationToken)
  {
    HttpResponseMessage response;
    try
    {
      response = await httpClient.GetAsync(new Uri(cin, UriKind.Relative), cancellationToken);
    }
    catch (HttpRequestException)
    {
      return Result.Failure<LegalEntityFinderResponse>(LegalEntityErrors.AresUnavailable);
    }
    catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
      return Result.Failure<LegalEntityFinderResponse>(LegalEntityErrors.AresUnavailable);
    }

    if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
    {
      return Result.Failure<LegalEntityFinderResponse>(LegalEntityErrors.NotFoundInAres(cin));
    }

    if (!response.IsSuccessStatusCode)
    {
      return Result.Failure<LegalEntityFinderResponse>(LegalEntityErrors.AresUnavailable);
    }

    AresEkonomickySubjektDto? dto;
    try
    {
      dto = await response.Content.ReadFromJsonAsync<AresEkonomickySubjektDto>(cancellationToken);
    }
    catch (JsonException)
    {
      return Result.Failure<LegalEntityFinderResponse>(LegalEntityErrors.AresUnavailable);
    }

    if (dto is null)
    {
      return Result.Failure<LegalEntityFinderResponse>(LegalEntityErrors.AresUnavailable);
    }

    return new LegalEntityFinderResponse(
      Name: dto.ObchodniJmeno,
      Cin: dto.Ico,
      Tin: dto.Dic,
      Address: new AresAddressResponse(
        CountryCode: dto.Sidlo.KodStatu,
        City: dto.Sidlo.NazevObce,
        ZipCode: dto.Sidlo.Psc.ToString("D5", CultureInfo.InvariantCulture),
        Street: dto.Sidlo.NazevUlice,
        HouseNumber: dto.Sidlo.CisloDomovni.ToString(CultureInfo.InvariantCulture)));
  }
}
