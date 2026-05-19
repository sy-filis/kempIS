using Application.Abstractions.Authentication;
using Domain.Services.Languages;
using Domain.Services.Services;
using Domain.Services.ServiceTexts;
using Domain.Services.ServiceTypes;
using Domain.Services.VatRates;
using Microsoft.EntityFrameworkCore;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Services;

public sealed class VatRatesEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public VatRatesEndpointTests(ApiFactory factory) => _factory = factory;

  public Task InitializeAsync() => _factory.ResetAllAsync();
  public Task DisposeAsync() => Task.CompletedTask;

  private HttpClient Client(params string[] roles)
  {
    HttpClient client = _factory.CreateClient();
    if (roles.Length > 0)
    {
      client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(",", roles));
    }
    return client;
  }

  [Fact]
  public async Task Crud_Manager_Roundtrip_Succeeds()
  {
    HttpClient client = Client(Roles.Manager);

    HttpResponseMessage postResponse = await client.PostAsJsonAsync(
      new Uri("vat-rates", UriKind.Relative), new { Name = "Standard", Rate = 21m, IsActive = true });
    postResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
    Guid id = await postResponse.Content.ReadFromJsonAsync<Guid>();

    HttpResponseMessage getResponse = await client.GetAsync(new Uri("vat-rates", UriKind.Relative));
    getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    (await getResponse.Content.ReadAsStringAsync()).ShouldContain("Standard");

    HttpResponseMessage putResponse = await client.PutAsJsonAsync(
      new Uri($"vat-rates/{id}", UriKind.Relative), new { Name = "Reduced", Rate = 10m, IsActive = true });
    putResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

    HttpResponseMessage deleteResponse = await client.DeleteAsync(new Uri($"vat-rates/{id}", UriKind.Relative));
    deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
  }

  [Fact]
  public async Task GetVatRates_NoAuth_Returns401()
  {
    HttpResponseMessage response = await Client().GetAsync(new Uri("vat-rates", UriKind.Relative));
    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task PostVatRate_Receptionist_Returns403()
  {
    HttpResponseMessage response = await Client(Roles.Receptionist).PostAsJsonAsync(
      new Uri("vat-rates", UriKind.Relative), new { Name = "X", Rate = 0m, IsActive = true });
    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }
}

public sealed class LanguagesEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public LanguagesEndpointTests(ApiFactory factory) => _factory = factory;

  public Task InitializeAsync() => _factory.ResetAllAsync();
  public Task DisposeAsync() => Task.CompletedTask;

  private HttpClient Client(params string[] roles)
  {
    HttpClient client = _factory.CreateClient();
    if (roles.Length > 0)
    {
      client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(",", roles));
    }
    return client;
  }

  [Fact]
  public async Task Crud_Manager_Roundtrip_Succeeds()
  {
    HttpClient client = Client(Roles.Manager);

    HttpResponseMessage postResponse = await client.PostAsJsonAsync(
      new Uri("languages", UriKind.Relative), new { Code = "ja", Name = "Japanese" });
    postResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
    Guid id = await postResponse.Content.ReadFromJsonAsync<Guid>();

    HttpResponseMessage getResponse = await client.GetAsync(new Uri("languages", UriKind.Relative));
    getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

    HttpResponseMessage putResponse = await client.PutAsJsonAsync(
      new Uri($"languages/{id}", UriKind.Relative), new { Code = "ja", Name = "Japanese (updated)" });
    putResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

    HttpResponseMessage deleteResponse = await client.DeleteAsync(new Uri($"languages/{id}", UriKind.Relative));
    deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
  }

  [Fact]
  public async Task GetLanguages_NoAuth_Returns401()
  {
    HttpResponseMessage response = await Client().GetAsync(new Uri("languages", UriKind.Relative));
    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }
}

public sealed class ServiceTypesEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public ServiceTypesEndpointTests(ApiFactory factory) => _factory = factory;

  public Task InitializeAsync() => _factory.ResetAllAsync();
  public Task DisposeAsync() => Task.CompletedTask;

  private HttpClient Client(params string[] roles)
  {
    HttpClient client = _factory.CreateClient();
    if (roles.Length > 0)
    {
      client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(",", roles));
    }
    return client;
  }

  [Fact]
  public async Task Crud_Manager_Roundtrip_Succeeds()
  {
    HttpClient client = Client(Roles.Manager);

    HttpResponseMessage postResponse = await client.PostAsJsonAsync(
      new Uri("service-types", UriKind.Relative), new { Name = "Accommodation", IsActive = true });
    postResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
    Guid id = await postResponse.Content.ReadFromJsonAsync<Guid>();

    HttpResponseMessage getResponse = await client.GetAsync(new Uri("service-types", UriKind.Relative));
    getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

    HttpResponseMessage putResponse = await client.PutAsJsonAsync(
      new Uri($"service-types/{id}", UriKind.Relative), new { Name = "Accommodation2", IsActive = false });
    putResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

    HttpResponseMessage deleteResponse = await client.DeleteAsync(new Uri($"service-types/{id}", UriKind.Relative));
    deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
  }

  [Fact]
  public async Task GetServiceTypes_NoAuth_Returns401()
  {
    HttpResponseMessage response = await Client().GetAsync(new Uri("service-types", UriKind.Relative));
    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }
}

public sealed class ServicesEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public ServicesEndpointTests(ApiFactory factory) => _factory = factory;

  public Task InitializeAsync() => _factory.ResetAllAsync();
  public Task DisposeAsync() => Task.CompletedTask;

  private HttpClient Client(params string[] roles)
  {
    HttpClient client = _factory.CreateClient();
    if (roles.Length > 0)
    {
      client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(",", roles));
    }
    return client;
  }

  private static object Request(ServiceGroup serviceGroup, Guid serviceTypeId, Guid vatRateId, string name = "Tent Spot", decimal price = 100m) => new
  {
    ServiceGroup = serviceGroup,
    ServiceTypeId = serviceTypeId,
    VatRateId = vatRateId,
    Name = name,
    BasePrice = price,
    IsActive = true,
  };

  [Fact]
  public async Task Crud_Manager_Roundtrip_Succeeds()
  {
    HttpClient client = Client(Roles.Manager);

    var typeId = Guid.NewGuid();
    var vatId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.ServiceTypes.Add(new ServiceType { Id = typeId, Name = "Accommodation", IsActive = true });
      db.VatRates.Add(new VatRate { Id = vatId, Name = "Standard", Rate = 21m, IsActive = true });
      await db.SaveChangesAsync();
    });

    HttpResponseMessage postResponse = await client.PostAsJsonAsync(
      new Uri("services", UriKind.Relative), Request(ServiceGroup.Spots, typeId, vatId));
    postResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
    Guid id = await postResponse.Content.ReadFromJsonAsync<Guid>();

    HttpResponseMessage getResponse = await client.GetAsync(new Uri("services", UriKind.Relative));
    getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

    HttpResponseMessage putResponse = await client.PutAsJsonAsync(
      new Uri($"services/{id}", UriKind.Relative), Request(ServiceGroup.Spots, typeId, vatId, name: "Renamed", price: 200m));
    putResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

    HttpResponseMessage deleteResponse = await client.DeleteAsync(new Uri($"services/{id}", UriKind.Relative));
    deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
  }

  [Fact]
  public async Task GetServices_NoAuth_Returns401()
  {
    HttpResponseMessage response = await Client().GetAsync(new Uri("services", UriKind.Relative));
    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task PostService_Receptionist_Returns403()
  {
    HttpResponseMessage response = await Client(Roles.Receptionist).PostAsJsonAsync(
      new Uri("services", UriKind.Relative), Request(ServiceGroup.Spots, Guid.NewGuid(), Guid.NewGuid()));
    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task PostService_UnknownServiceType_Returns404()
  {
    HttpClient client = Client(Roles.Manager);

    var vatId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.VatRates.Add(new VatRate { Id = vatId, Name = "Standard", Rate = 21m, IsActive = true });
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await client.PostAsJsonAsync(
      new Uri("services", UriKind.Relative), Request(ServiceGroup.Spots, Guid.NewGuid(), vatId));

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task PostService_UnknownVatRate_Returns404()
  {
    HttpClient client = Client(Roles.Manager);

    var typeId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.ServiceTypes.Add(new ServiceType { Id = typeId, Name = "Accommodation", IsActive = true });
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await client.PostAsJsonAsync(
      new Uri("services", UriKind.Relative), Request(ServiceGroup.Spots, typeId, Guid.NewGuid()));

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task PutService_UnknownServiceType_Returns404()
  {
    HttpClient client = Client(Roles.Manager);

    var serviceId = Guid.NewGuid();
    var typeId = Guid.NewGuid();
    var vatId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.ServiceTypes.Add(new ServiceType { Id = typeId, Name = "Accommodation", IsActive = true });
      db.VatRates.Add(new VatRate { Id = vatId, Name = "Standard", Rate = 21m, IsActive = true });
      db.Services.Add(new Service
      {
        Id = serviceId,
        ServiceGroup = ServiceGroup.Spots,
        ServiceTypeId = typeId,
        VatRateId = vatId,
        Name = "Tent Spot",
        BasePrice = 100m,
        IsActive = true,
      });
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await client.PutAsJsonAsync(
      new Uri($"services/{serviceId}", UriKind.Relative), Request(ServiceGroup.Spots, Guid.NewGuid(), vatId));

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task PutService_UnknownVatRate_Returns404()
  {
    HttpClient client = Client(Roles.Manager);

    var serviceId = Guid.NewGuid();
    var typeId = Guid.NewGuid();
    var vatId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.ServiceTypes.Add(new ServiceType { Id = typeId, Name = "Accommodation", IsActive = true });
      db.VatRates.Add(new VatRate { Id = vatId, Name = "Standard", Rate = 21m, IsActive = true });
      db.Services.Add(new Service
      {
        Id = serviceId,
        ServiceGroup = ServiceGroup.Spots,
        ServiceTypeId = typeId,
        VatRateId = vatId,
        Name = "Tent Spot",
        BasePrice = 100m,
        IsActive = true,
      });
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await client.PutAsJsonAsync(
      new Uri($"services/{serviceId}", UriKind.Relative), Request(ServiceGroup.Spots, typeId, Guid.NewGuid()));

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }
}

public sealed class ServiceTextsEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public ServiceTextsEndpointTests(ApiFactory factory) => _factory = factory;

  public Task InitializeAsync() => _factory.ResetAllAsync();
  public Task DisposeAsync() => Task.CompletedTask;

  private HttpClient Client(params string[] roles)
  {
    HttpClient client = _factory.CreateClient();
    if (roles.Length > 0)
    {
      client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(",", roles));
    }
    return client;
  }

  private static object Request(Guid serviceId, Guid languageId, string text = "Standard tent site with water") => new
  {
    ServiceId = serviceId,
    LanguageId = languageId,
    PrintText = text,
  };

  [Fact]
  public async Task Crud_Manager_Roundtrip_Succeeds()
  {
    HttpClient client = Client(Roles.Manager);

    var serviceId = Guid.NewGuid();
    Guid languageId = Guid.Empty;
    await _factory.WithDbAsync(async db =>
    {
      var typeId = Guid.NewGuid();
      var vatId = Guid.NewGuid();
      db.ServiceTypes.Add(new ServiceType { Id = typeId, Name = "Accommodation", IsActive = true });
      db.VatRates.Add(new VatRate { Id = vatId, Name = "Standard", Rate = 21m, IsActive = true });
      db.Services.Add(new Service
      {
        Id = serviceId,
        ServiceGroup = ServiceGroup.Spots,
        ServiceTypeId = typeId,
        VatRateId = vatId,
        Name = "Tent Spot",
        BasePrice = 100m,
        IsActive = true,
      });
      languageId = await db.Languages.Where(l => l.Code == "en").Select(l => l.Id).SingleAsync();
      await db.SaveChangesAsync();
    });

    HttpResponseMessage postResponse = await client.PostAsJsonAsync(
      new Uri("service-texts", UriKind.Relative), Request(serviceId, languageId));
    postResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
    Guid id = await postResponse.Content.ReadFromJsonAsync<Guid>();

    HttpResponseMessage getResponse = await client.GetAsync(new Uri("service-texts", UriKind.Relative));
    getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

    HttpResponseMessage putResponse = await client.PutAsJsonAsync(
      new Uri($"service-texts/{id}", UriKind.Relative), Request(serviceId, languageId, text: "Updated"));
    putResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

    HttpResponseMessage deleteResponse = await client.DeleteAsync(new Uri($"service-texts/{id}", UriKind.Relative));
    deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
  }

  [Fact]
  public async Task GetServiceTexts_NoAuth_Returns401()
  {
    HttpResponseMessage response = await Client().GetAsync(new Uri("service-texts", UriKind.Relative));
    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task PostServiceText_UnknownService_Returns404()
  {
    HttpClient client = Client(Roles.Manager);

    Guid languageId = Guid.Empty;
    await _factory.WithDbAsync(async db =>
    {
      languageId = await db.Languages.Where(l => l.Code == "en").Select(l => l.Id).SingleAsync();
    });

    HttpResponseMessage response = await client.PostAsJsonAsync(
      new Uri("service-texts", UriKind.Relative), Request(Guid.NewGuid(), languageId));

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task PostServiceText_UnknownLanguage_Returns404()
  {
    HttpClient client = Client(Roles.Manager);

    var serviceId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      var typeId = Guid.NewGuid();
      var vatId = Guid.NewGuid();
      db.ServiceTypes.Add(new ServiceType { Id = typeId, Name = "Accommodation", IsActive = true });
      db.VatRates.Add(new VatRate { Id = vatId, Name = "Standard", Rate = 21m, IsActive = true });
      db.Services.Add(new Service
      {
        Id = serviceId,
        ServiceGroup = ServiceGroup.Spots,
        ServiceTypeId = typeId,
        VatRateId = vatId,
        Name = "Tent Spot",
        BasePrice = 100m,
        IsActive = true,
      });
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await client.PostAsJsonAsync(
      new Uri("service-texts", UriKind.Relative), Request(serviceId, Guid.NewGuid()));

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task PutServiceText_UnknownService_Returns404()
  {
    HttpClient client = Client(Roles.Manager);

    var textId = Guid.NewGuid();
    var serviceId = Guid.NewGuid();
    Guid languageId = Guid.Empty;
    await _factory.WithDbAsync(async db =>
    {
      var typeId = Guid.NewGuid();
      var vatId = Guid.NewGuid();
      db.ServiceTypes.Add(new ServiceType { Id = typeId, Name = "Accommodation", IsActive = true });
      db.VatRates.Add(new VatRate { Id = vatId, Name = "Standard", Rate = 21m, IsActive = true });
      db.Services.Add(new Service
      {
        Id = serviceId,
        ServiceGroup = ServiceGroup.Spots,
        ServiceTypeId = typeId,
        VatRateId = vatId,
        Name = "Tent Spot",
        BasePrice = 100m,
        IsActive = true,
      });
      languageId = await db.Languages.Where(l => l.Code == "en").Select(l => l.Id).SingleAsync();
      db.ServiceTexts.Add(new ServiceText
      {
        Id = textId,
        ServiceId = serviceId,
        LanguageId = languageId,
        PrintText = "Standard tent site with water",
      });
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await client.PutAsJsonAsync(
      new Uri($"service-texts/{textId}", UriKind.Relative), Request(Guid.NewGuid(), languageId));

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task PutServiceText_UnknownLanguage_Returns404()
  {
    HttpClient client = Client(Roles.Manager);

    var textId = Guid.NewGuid();
    var serviceId = Guid.NewGuid();
    Guid languageId = Guid.Empty;
    await _factory.WithDbAsync(async db =>
    {
      var typeId = Guid.NewGuid();
      var vatId = Guid.NewGuid();
      db.ServiceTypes.Add(new ServiceType { Id = typeId, Name = "Accommodation", IsActive = true });
      db.VatRates.Add(new VatRate { Id = vatId, Name = "Standard", Rate = 21m, IsActive = true });
      db.Services.Add(new Service
      {
        Id = serviceId,
        ServiceGroup = ServiceGroup.Spots,
        ServiceTypeId = typeId,
        VatRateId = vatId,
        Name = "Tent Spot",
        BasePrice = 100m,
        IsActive = true,
      });
      languageId = await db.Languages.Where(l => l.Code == "en").Select(l => l.Id).SingleAsync();
      db.ServiceTexts.Add(new ServiceText
      {
        Id = textId,
        ServiceId = serviceId,
        LanguageId = languageId,
        PrintText = "Standard tent site with water",
      });
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await client.PutAsJsonAsync(
      new Uri($"service-texts/{textId}", UriKind.Relative), Request(serviceId, Guid.NewGuid()));

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }
}
