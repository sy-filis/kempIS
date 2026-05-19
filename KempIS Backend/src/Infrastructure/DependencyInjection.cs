using System.Net;
using System.Security.Cryptography.X509Certificates;
using Application.Abstractions.Addresses;
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Documents;
using Application.Abstractions.EDoklady;
using Application.Abstractions.Email;
using Application.Abstractions.Finance;
using Application.Abstractions.Gate;
using Application.Abstractions.Reception;
using Application.Abstractions.Reservations;
using Application.Finance.Bills;
using Application.Finance.FinancialClosings;
using Application.Reception.Realtime;
using Infrastructure.Authentication;
using Infrastructure.Caching;
using Infrastructure.Database;
using Infrastructure.Documents.Bills;
using Infrastructure.Documents.Bills.Resources;
using Infrastructure.Documents.FinancialClosings;
using Infrastructure.Documents.Pdf;
using Infrastructure.Documents.Qr;
using Infrastructure.DomainEvents;
using Infrastructure.Email;
using Infrastructure.ExternalServices.Ares;
using Infrastructure.ExternalServices.EDoklady;
using Infrastructure.ExternalServices.Gate;
using Infrastructure.ExternalServices.Mapy;
using Infrastructure.ExternalServices.Ruian;
using Infrastructure.ExternalServices.Ubyport;
using Infrastructure.Finance;
using Infrastructure.Identity;
using Infrastructure.Reception;
using Infrastructure.Reservations;
using Infrastructure.Time;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using RazorLight;
using SharedKernel;

namespace Infrastructure;

public static class DependencyInjection
{
  public static IServiceCollection AddInfrastructure(
      this IServiceCollection services,
      IConfiguration configuration) =>
      services
          .AddServices()
          .AddDatabase(configuration)
          .AddIdentityInternal(configuration)
          .AddHealthChecks(configuration)
          .AddAuthenticationInternal(configuration)
          .AddAuthorizationInternal()
          .AddEmail(configuration)
          .AddExternalServices(configuration)
          .AddDocuments();

  private static IServiceCollection AddEmail(this IServiceCollection services, IConfiguration configuration)
  {
    services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
    services.Configure<EmailTemplateOptions>(configuration.GetSection(EmailTemplateOptions.SectionName));
    services.AddTransient<Application.Abstractions.Email.IEmailSender, SmtpEmailSender>();
    services.AddSingleton<IEmailTemplateRenderer, FileEmailTemplateRenderer>();
    return services;
  }

  private static IServiceCollection AddServices(this IServiceCollection services)
  {
    services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

    services.AddTransient<IDomainEventsDispatcher, DomainEventsDispatcher>();

    services.AddScoped<IBillNumberGenerator, BillNumberGenerator>();
    services.AddScoped<IReservationNumberGenerator, ReservationNumberGenerator>();
    services.AddScoped<IGroupReservationNumberGenerator, GroupReservationNumberGenerator>();

    services.AddHostedService<Retention.RetentionScheduler>();

    return services;
  }

  private static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
  {
    string? connectionString = configuration.GetConnectionString("Database");

    services.AddDbContext<ApplicationDbContext>(
        options => options
            .UseNpgsql(connectionString, npgsqlOptions =>
                npgsqlOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Default))
            .UseSnakeCaseNamingConvention());

    services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

    return services;
  }

  private static IServiceCollection AddHealthChecks(this IServiceCollection services, IConfiguration configuration)
  {
    services
        .AddHealthChecks()
        .AddNpgSql(configuration.GetConnectionString("Database")!);

    return services;
  }

  private static IServiceCollection AddIdentityInternal(
      this IServiceCollection services,
      IConfiguration configuration)
  {
    services
        .AddIdentityCore<ApplicationUser>(options =>
        {
          options.User.RequireUniqueEmail = false;
          // Required so EF stores include AspNetUserPasskeys for passkey APIs.
          options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
        })
        .AddRoles<ApplicationRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders()
        .AddPasskeys();

    services.Configure<IdentityPasskeyOptions>(options =>
    {
      options.ServerDomain = configuration["Identity:Passkeys:ServerDomain"];

      string[] allowedOrigins = configuration
              .GetSection("Identity:Passkeys:AllowedOrigins")
              .Get<string[]>() ?? [];

      if (allowedOrigins.Length > 0)
      {
        options.ValidateOrigin = ctx => ValueTask.FromResult(
                allowedOrigins.Contains(ctx.Origin, StringComparer.OrdinalIgnoreCase));
      }
    });

    return services;
  }

  private static IServiceCollection AddAuthenticationInternal(
      this IServiceCollection services,
      IConfiguration configuration)
  {
    BearerTokenSettings bearerTokenSettings =
        configuration.GetSection(BearerTokenSettings.SectionName).Get<BearerTokenSettings>()
        ?? new BearerTokenSettings();

    services.AddSingleton(bearerTokenSettings);

    services.AddAuthentication(IdentityConstants.BearerScheme)
        .AddBearerToken(IdentityConstants.BearerScheme, options =>
        {
          options.BearerTokenExpiration = TimeSpan.FromMinutes((double)bearerTokenSettings.AccessTokenExpirationMinutes);
          options.RefreshTokenExpiration = TimeSpan.FromMinutes((double)bearerTokenSettings.RefreshTokenExpirationMinutes);
        })
        // Required: SignInManager.MakePasskeyRequestOptionsAsync persists challenge state
        // via this cookie scheme, otherwise the login challenge endpoint throws 500.
        .AddCookie(IdentityConstants.TwoFactorUserIdScheme, options =>
        {
          options.Cookie.Name = IdentityConstants.TwoFactorUserIdScheme;
          options.Cookie.HttpOnly = true;
          options.Cookie.SameSite = SameSiteMode.Strict;
          options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
          options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
          options.SlidingExpiration = false;
        });

    services.AddHttpContextAccessor();
    services.AddScoped<IUserContext, UserContext>();
    services.AddScoped<IPasskeyAuthenticator, PasskeyAuthenticator>();
    services.AddScoped<IIdentityService, IdentityService>();

    return services;
  }

  private static IServiceCollection AddAuthorizationInternal(this IServiceCollection services)
  {
    services.AddAuthorization();

    return services;
  }

  private static IServiceCollection AddExternalServices(
      this IServiceCollection services,
      IConfiguration configuration)
  {
    string aresBaseUrl = configuration["Ares:BaseUrl"]
        ?? throw new InvalidOperationException("Missing Ares:BaseUrl configuration.");

    services.AddHttpClient<ILegalEntityFinder, AresLegalEntityFinder>(c =>
    {
      c.BaseAddress = new Uri(aresBaseUrl);
      c.Timeout = TimeSpan.FromSeconds(5);
    });

    string redisConnection = configuration.GetConnectionString("Redis")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:Redis configuration.");

    services.AddStackExchangeRedisCache(options =>
    {
      options.Configuration = redisConnection;
      options.InstanceName = "kempis:";
    });
    services.AddSingleton<IAddressSuggestionCache, RedisAddressSuggestionCache>();

    string ruianBaseUrl = configuration["Ruian:BaseUrl"]
        ?? throw new InvalidOperationException("Missing Ruian:BaseUrl configuration.");

    services.AddHttpClient<RuianAddressSuggester>(c =>
    {
      c.BaseAddress = new Uri(ruianBaseUrl);
      c.Timeout = TimeSpan.FromSeconds(5);
    });
    services.AddKeyedScoped<IAddressSuggester>(
        AddressProvider.Ruian,
        (sp, _) => sp.GetRequiredService<RuianAddressSuggester>());

    services.Configure<MapyOptions>(configuration.GetSection(MapyOptions.SectionName));
    MapyOptions mapyOptions = configuration.GetSection(MapyOptions.SectionName).Get<MapyOptions>()
        ?? throw new InvalidOperationException("Missing Mapy configuration.");
    if (string.IsNullOrWhiteSpace(mapyOptions.BaseUrl))
    {
      throw new InvalidOperationException("Missing Mapy:BaseUrl configuration.");
    }
    if (string.IsNullOrWhiteSpace(mapyOptions.ApiKey))
    {
      throw new InvalidOperationException("Missing Mapy:ApiKey configuration.");
    }

    services.AddHttpClient<MapyAddressSuggester>(c =>
    {
      c.BaseAddress = new Uri(mapyOptions.BaseUrl);
      c.Timeout = TimeSpan.FromSeconds(5);
    });
    services.AddKeyedScoped<IAddressSuggester>(
        AddressProvider.Mapy,
        (sp, _) => sp.GetRequiredService<MapyAddressSuggester>());

    services.AddOptions<UbyportOptions>()
        .Bind(configuration.GetSection(UbyportOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    services.AddHttpClient<IPoliceGuestReporter, UbyportPoliceGuestReporter>((sp, c) =>
    {
      UbyportOptions o = sp.GetRequiredService<IOptions<UbyportOptions>>().Value;
      c.BaseAddress = new Uri(o.EndpointUrl);
      c.Timeout = TimeSpan.FromSeconds(30);
    })
    .ConfigurePrimaryHttpMessageHandler(sp =>
    {
      UbyportOptions o = sp.GetRequiredService<IOptions<UbyportOptions>>().Value;
      return new HttpClientHandler
      {
        Credentials = new NetworkCredential(o.Username, o.Password),
        PreAuthenticate = true
      };
    });

    services.AddOptions<EDokladyOptions>()
        .Bind(configuration.GetSection(EDokladyOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    services.AddHttpClient<IEDokladyClient, EDokladyClient>((sp, c) =>
    {
      EDokladyOptions o = sp.GetRequiredService<IOptions<EDokladyOptions>>().Value;
      c.BaseAddress = new Uri(o.BaseUrl);
      c.Timeout = TimeSpan.FromSeconds(30);
    })
    .ConfigurePrimaryHttpMessageHandler(sp =>
    {
      EDokladyOptions o = sp.GetRequiredService<IOptions<EDokladyOptions>>().Value;
      X509Certificate2 cert = X509CertificateLoader.LoadPkcs12FromFile(
              o.Certificate.PfxPath, (string?)o.Certificate.PfxPassword);
      HttpClientHandler handler = new() { ClientCertificateOptions = ClientCertificateOption.Manual };
      handler.ClientCertificates.Add(cert);
      return handler;
    });

    services.AddOptions<ReceptionOptions>()
        .Bind(configuration.GetSection(ReceptionOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    GateSystemOptions? gateOptions = configuration.GetSection(GateSystemOptions.SectionName).Get<GateSystemOptions>();
    if (string.IsNullOrWhiteSpace(gateOptions?.BaseUrl))
    {
      services.AddSingleton<IGateClient, NoOpGateClient>();
    }
    else
    {
      services.Configure<GateSystemOptions>(configuration.GetSection(GateSystemOptions.SectionName));
      services.AddHttpClient<IGateClient, HttpGateClient>((sp, c) =>
      {
        GateSystemOptions o = sp.GetRequiredService<IOptions<GateSystemOptions>>().Value;
        c.BaseAddress = new Uri(o.BaseUrl!);
        c.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds > 0 ? o.TimeoutSeconds : 5);
      });
    }

    services.AddSingleton<IReceptionRealtimeCoordinator, InMemoryReceptionRealtimeCoordinator>();

    return services;
  }

  private static IServiceCollection AddDocuments(this IServiceCollection services)
  {
    services.AddSingleton<IQrCodeEncoder, QrCoderQrCodeEncoder>();

    services.AddLocalization();

    services.AddSingleton<RazorLightEngine>(_ =>
        new RazorLightEngineBuilder()
            .UseEmbeddedResourcesProject(typeof(BillResources).Assembly, "Infrastructure.Documents")
            .UseMemoryCachingProvider()
            .Build());

    services.AddSingleton<IPlaywright>(_ =>
        Microsoft.Playwright.Playwright.CreateAsync().GetAwaiter().GetResult());

    services.AddSingleton<IBrowser>(sp =>
        sp.GetRequiredService<IPlaywright>()
            .Chromium
            .LaunchAsync(new BrowserTypeLaunchOptions { Headless = true })
            .GetAwaiter().GetResult());

    services.AddSingleton<IPdfRenderer, PlaywrightPdfRenderer>();
    services.AddScoped<IBillDocumentRenderer, RazorLightBillDocumentRenderer>();
    services.AddScoped<IBillStickerRenderer, RazorLightBillStickerRenderer>();
    services.AddScoped<IFinancialClosingReportRenderer, RazorLightFinancialClosingReportRenderer>();

    return services;
  }
}
