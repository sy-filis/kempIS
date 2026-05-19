using System.Collections.Concurrent;
using Application.Abstractions.Addresses;
using Application.Abstractions.Authentication;
using Application.Abstractions.EDoklady;
using Application.Abstractions.Email;
using Application.Abstractions.Finance;
using Application.Abstractions.Gate;
using Application.Abstractions.Reservations;
using Application.Finance.FinancialClosings;
using Infrastructure.Database;
using Infrastructure.Seed;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using TestUtilities.Fakes;

namespace Web.Api.IntegrationTests.Infrastructure;

public class ApiFactory : WebApplicationFactory<Program>
{
  private readonly SqliteConnection _connection;

  public FakePasskeyAuthenticator PasskeyAuthenticator { get; } = new();
  public CapturingEmailSender EmailSender { get; } = new();
  public StubIdentityService IdentityService { get; } = new();
  public ISpotAvailabilityChecker AvailabilityChecker { get; } = Substitute.For<ISpotAvailabilityChecker>();
  public ILegalEntityFinder LegalEntityFinder { get; } = Substitute.For<ILegalEntityFinder>();
  public IPoliceGuestReporter PoliceGuestReporter { get; } = Substitute.For<IPoliceGuestReporter>();
  public IEDokladyClient EDokladyClient { get; } = Substitute.For<IEDokladyClient>();
  public IGateClient GateClient { get; } = Substitute.For<IGateClient>();
  public InMemoryAddressSuggestionCache AddressCache { get; } = new();
  public IAddressSuggester RuianSuggester { get; } = Substitute.For<IAddressSuggester>();
  public IAddressSuggester MapySuggester { get; } = Substitute.For<IAddressSuggester>();
  public IFinancialClosingReportRenderer FinancialClosingReportRenderer { get; } = Substitute.For<IFinancialClosingReportRenderer>();
  public ConcurrentQueue<Exception> ServerExceptions { get; } = new();

  protected virtual bool SubstituteFinancialClosingRenderer => true;

  public ApiFactory()
  {
    byte[] stubPdf = new byte[2048];
    System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n").CopyTo(stubPdf, 0);
    FinancialClosingReportRenderer
      .RenderAsync(Arg.Any<Domain.Finance.FinancialClosings.FinancialClosing>(), Arg.Any<CancellationToken>())
      .Returns(SharedKernel.Result.Success(stubPdf));

    _connection = new SqliteConnection("DataSource=:memory:");
    _connection.Open();
    using (SqliteCommand pragma = _connection.CreateCommand())
    {
      pragma.CommandText = "PRAGMA foreign_keys = OFF;";
      pragma.ExecuteNonQuery();
    }

    DbContextOptions<ApplicationDbContext> options =
      new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlite(_connection)
        .UseSnakeCaseNamingConvention()
        .Options;

    using var initContext = new ApplicationDbContext(options, NullDomainEventsDispatcher.Instance, new TestUtilities.Fakes.FakeDateTimeProvider());
    initContext.Database.EnsureCreated();

    ClientOptions.BaseAddress = new Uri("http://localhost/api/");
  }

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.UseEnvironment("Test");

    builder.ConfigureAppConfiguration((_, config) =>
    {
      config.AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["ConnectionStrings:Database"] = "DataSource=:memory:",
        ["Email:Host"] = "localhost",
        ["Email:Port"] = "25",
        ["Email:From"] = "test@example.com",
        ["Email:FromName"] = "Test",
        ["Ares:BaseUrl"] = "https://ares.test/ekonomicke-subjekty/",
        ["ConnectionStrings:Redis"] = "localhost:6379",
        ["Ruian:BaseUrl"] = "https://ruian.test/geocode/",
        ["Mapy:BaseUrl"] = "https://api.mapy.test/v1/",
        ["Mapy:ApiKey"] = "test-key",
        ["Ubyport:EndpointUrl"] = "https://ubyport.pcr.cz/ws_uby_test/ws_uby.svc",
        ["Ubyport:Username"] = "u",
        ["Ubyport:Password"] = "p",
        ["Ubyport:AuthenticationCode"] = "code",
        ["Ubyport:IdUb"] = "1",
        ["Ubyport:Mark"] = "MARK",
        ["Ubyport:Name"] = "Test Accommodation",
        ["Ubyport:Contact"] = "test@example.com",
        ["Ubyport:District"] = "Praha",
        ["Ubyport:Town"] = "Praha",
        ["Ubyport:Street"] = "Lennonova",
        ["Ubyport:HouseNumber"] = "1",
        ["Ubyport:Zip"] = "10000",
        ["Camp:CheckOutTime"] = "11:00:00",
        ["Camp:Name"] = "ATC Olšovec",
        ["Camp:Street"] = "Kopeček",
        ["Camp:City"] = "Jedovnice",
        ["Camp:ZipCode"] = "679 06",
        ["Camp:Cin"] = "12345678",
        ["Camp:Tin"] = "CZ12345678",
        ["Camp:Phone"] = "+420 516 442 216",
        ["Camp:Email"] = "info@atcolsovec.cz",
        ["Camp:Web"] = "https://www.atcolsovec.cz",
        ["Retention:GuestYears"] = "6",
        ["Retention:BillYears"] = "10",
        ["Retention:InvoiceYears"] = "10",
        ["Retention:RunAtLocalTime"] = "03:00:00",
        ["Frontend:BaseUrl"] = "http://localhost",
        ["EDoklady:BaseUrl"] = "https://edoklady.test/",
        ["EDoklady:Certificate:PfxPath"] = "test.pfx",
        ["EDoklady:Certificate:PfxPassword"] = "test",
        ["EDoklady:QrCodeRefreshThresholdDays"] = "7",
        ["Reception:PairCodeTtlSeconds"] = "120",
        ["Reception:TabletJoinGraceSeconds"] = "10",
        ["Reception:SessionPushMaxBytes"] = "65536",
        ["Reception:SignaturePngMaxBytes"] = "262144",
        ["Reception:DefaultEventMaxBytes"] = "16384",
        ["Reception:AllowlistSweepIntervalSeconds"] = "60",
      });
    });

    builder.ConfigureServices(services =>
    {
      services.RemoveAll<IHostedService>();

      // Strip Npgsql IDbContextOptionsConfiguration before registering SQLite to avoid
      // a two-providers conflict from AddDbContext appending configuration delegates.
      services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
      services.RemoveAll<DbContextOptions>();
      services.RemoveAll<ApplicationDbContext>();
      services.RemoveAll(typeof(Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<ApplicationDbContext>));
      services.AddDbContext<ApplicationDbContext>(o => o
          .UseSqlite(_connection)
          .UseSnakeCaseNamingConvention());

      services.Configure<AuthenticationOptions>(o =>
      {
        o.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
        o.DefaultChallengeScheme = TestAuthHandler.SchemeName;
        o.DefaultScheme = TestAuthHandler.SchemeName;
      });
      services.AddAuthentication(TestAuthHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

      services.Replace(ServiceDescriptor.Scoped<IPasskeyAuthenticator>(_ => PasskeyAuthenticator));
      services.Replace(ServiceDescriptor.Scoped<IIdentityService>(_ => IdentityService));
      services.Replace(ServiceDescriptor.Singleton<IEmailSender>(_ => EmailSender));
      IEmailTemplateRenderer renderer = Substitute.For<IEmailTemplateRenderer>();
      renderer.RenderAsync(default!, default!, default!, default)
        .ReturnsForAnyArgs(SharedKernel.Result.Success(new RenderedEmail("Test subject", "Test body")));
      services.Replace(ServiceDescriptor.Singleton(renderer));
      services.Replace(ServiceDescriptor.Scoped(_ => AvailabilityChecker));
      services.Replace(ServiceDescriptor.Scoped(_ => LegalEntityFinder));
      services.Replace(ServiceDescriptor.Scoped(_ => PoliceGuestReporter));
      if (SubstituteFinancialClosingRenderer)
      {
        services.Replace(ServiceDescriptor.Scoped(_ => FinancialClosingReportRenderer));
      }

      services.RemoveAll<IEDokladyClient>();
      services.AddScoped<IEDokladyClient>(_ => EDokladyClient);

      services.RemoveAll<IGateClient>();
      services.AddScoped<IGateClient>(_ => GateClient);

      services.RemoveAll<IAddressSuggestionCache>();
      services.AddSingleton<IAddressSuggestionCache>(AddressCache);

      services.RemoveAll<IAddressSuggester>();
      services.AddKeyedSingleton(AddressProvider.Ruian, (_, _) => RuianSuggester);
      services.AddKeyedSingleton(AddressProvider.Mapy, (_, _) => MapySuggester);

      services.RemoveAll<Microsoft.AspNetCore.Diagnostics.IExceptionHandler>();
      services.AddSingleton<Microsoft.AspNetCore.Diagnostics.IExceptionHandler>(
        _ => new ExceptionCapturingHandler(ServerExceptions));
    });
  }

  protected override void Dispose(bool disposing)
  {
    if (disposing)
    {
      _connection.Dispose();
    }
    base.Dispose(disposing);
  }

  public async Task WithDbAsync(Func<ApplicationDbContext, Task> action)
  {
    using IServiceScope scope = Services.CreateScope();
    ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await action(db);
  }

  public async Task ResetReservationsAsync()
  {
    await WithDbAsync(async db =>
    {
      db.ReservationSpotItems.RemoveRange(db.ReservationSpotItems);
      db.Reservations.RemoveRange(db.Reservations);
      db.GroupReservationSpots.RemoveRange(db.GroupReservationSpots);
      db.GroupReservations.RemoveRange(db.GroupReservations);
      db.Spots.RemoveRange(db.Spots);
      db.SpotGroups.RemoveRange(db.SpotGroups);
      db.SpotOofItems.RemoveRange(db.SpotOofItems);
      db.SpotGroupOofItems.RemoveRange(db.SpotGroupOofItems);
      db.OutOfOrders.RemoveRange(db.OutOfOrders);
      db.Events.RemoveRange(db.Events);
      await db.SaveChangesAsync();
    });
  }

  public async Task ResetAllAsync()
  {
    await WithDbAsync(async db =>
    {
      db.AccessCards.RemoveRange(db.AccessCards);
      db.Guests.RemoveRange(db.Guests);
      db.Vehicles.RemoveRange(db.Vehicles);
      db.Meals.RemoveRange(db.Meals);
      db.Nationalities.RemoveRange(db.Nationalities);
      db.ReservationServiceItems.RemoveRange(db.ReservationServiceItems);
      db.ReservationSpotItems.RemoveRange(db.ReservationSpotItems);
      db.Reservations.RemoveRange(db.Reservations);
      db.GroupReservationSpots.RemoveRange(db.GroupReservationSpots);
      db.GroupReservations.RemoveRange(db.GroupReservations);
      db.CleanInfos.RemoveRange(db.CleanInfos);
      db.CleaningPlans.RemoveRange(db.CleaningPlans);
      db.Spots.RemoveRange(db.Spots);
      db.SpotGroups.RemoveRange(db.SpotGroups);
      db.SpotOofItems.RemoveRange(db.SpotOofItems);
      db.SpotGroupOofItems.RemoveRange(db.SpotGroupOofItems);
      db.MaintenanceIssues.RemoveRange(db.MaintenanceIssues);
      db.OutOfOrders.RemoveRange(db.OutOfOrders);
      db.EventSpotGroupItems.RemoveRange(db.EventSpotGroupItems);
      db.Events.RemoveRange(db.Events);
      db.ServiceTexts.RemoveRange(db.ServiceTexts);
      db.Services.RemoveRange(db.Services);
      db.ServiceTypes.RemoveRange(db.ServiceTypes);
      db.VatRates.RemoveRange(db.VatRates);
      db.Languages.RemoveRange(db.Languages);
      await db.SaveChangesAsync();

      ReferenceDataSeeder seeder = new(db, NullLogger<ReferenceDataSeeder>.Instance);
      await seeder.SeedAsync(CancellationToken.None);
    });

    GateClient.ClearReceivedCalls();
  }
}
