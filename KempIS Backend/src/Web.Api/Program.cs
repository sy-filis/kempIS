using System.Reflection;
using Application;
using Application.Abstractions.Authentication;
using Application.Configuration;
using HealthChecks.UI.Client;
using Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Web.Api;
using Web.Api.Authentication;
using Web.Api.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

bool noAuth = args.Contains("--no-auth", StringComparer.OrdinalIgnoreCase);
if (noAuth && builder.Environment.IsProduction())
{
  throw new InvalidOperationException("--no-auth is forbidden in the Production environment.");
}

builder.Host.UseSerilog((context, loggerConfig) => loggerConfig.ReadFrom.Configuration(context.Configuration));

builder.Services.AddOpenApiWithAuth();

builder.Services
    .AddApplication()
    .AddPresentation()
    .AddInfrastructure(builder.Configuration)
    .AddConfiguredCors(builder.Configuration);

builder.Services
    .AddOptions<CampSettings>()
    .Bind(builder.Configuration.GetSection(CampSettings.SectionName))
    .Validate(o => o.CheckOutTime != default, "Camp:CheckOutTime must be configured to a non-default value.")
    .Validate(o => !string.IsNullOrWhiteSpace(o.Name), "Camp:Name must be configured.")
    .Validate(o => !string.IsNullOrWhiteSpace(o.Street), "Camp:Street must be configured.")
    .Validate(o => !string.IsNullOrWhiteSpace(o.City), "Camp:City must be configured.")
    .Validate(o => !string.IsNullOrWhiteSpace(o.ZipCode), "Camp:ZipCode must be configured.")
    .ValidateOnStart();

builder.Services
    .AddOptions<RetentionSettings>()
    .Bind(builder.Configuration.GetSection(RetentionSettings.SectionName))
    .Validate(o => o.GuestYears >= 1, "Retention:GuestYears must be >= 1.")
    .Validate(o => o.BillYears >= 1, "Retention:BillYears must be >= 1.")
    .Validate(o => o.InvoiceYears >= 1, "Retention:InvoiceYears must be >= 1.")
    .Validate(o => o.RunAtLocalTime != default, "Retention:RunAtLocalTime must be configured.")
    .ValidateOnStart();

builder.Services
    .AddOptions<FrontendOptions>()
    .Bind(builder.Configuration.GetSection(FrontendOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

NoAuthOptions noAuthOptions = new() { IsEnabled = noAuth };
builder.Services.AddSingleton(noAuthOptions);
builder.Services.AddSingleton<INoAuthState>(noAuthOptions);

if (noAuth)
{
  Log.Warning(
    "NO-AUTH MODE ACTIVE - every request is treated as Manager with all roles. " +
    "Use only for initial setup; restart without --no-auth before regular use.");

  builder.Services
    .AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, NoAuthHandler>(NoAuthHandler.SchemeName, _ => { });

  builder.Services.PostConfigure<AuthenticationOptions>(options =>
  {
    options.DefaultAuthenticateScheme = NoAuthHandler.SchemeName;
    options.DefaultChallengeScheme = NoAuthHandler.SchemeName;
    options.DefaultScheme = NoAuthHandler.SchemeName;
  });
}

builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());

WebApplication app = builder.Build();

app.MapEndpoints(app.MapGroup("api"));

if (app.Environment.IsDevelopment())
{
  app.UseOpenApiWithScalar();

  app.ApplyMigrations();
}

await app.SeedReferenceDataAsync();

if (app.Environment.IsDevelopment())
{
  await app.SeedTestDataAsync();
}

// Skip in Test: each WebApplicationFactory<Program> in the integration suite would
// otherwise launch playwright install concurrently, racing on %LOCALAPPDATA%\ms-playwright
// and exhausting the test host until an MSBuild worker dies (MSB4166).
if (!app.Environment.IsEnvironment("Test"))
{
  int chromiumExitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
  if (chromiumExitCode != 0)
  {
    throw new InvalidOperationException($"Playwright Chromium install failed with exit code {chromiumExitCode}.");
  }
}

await app.SeedRolesAsync();

app.MapHealthChecks("health", new HealthCheckOptions
{
  ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.UseRequestContextLogging();

app.UseSerilogRequestLogging();

app.UseExceptionHandler();

app.UseCors(Web.Api.Extensions.ServiceCollectionExtensions.CorsPolicyName);

app.UseWebSockets(new WebSocketOptions
{
  KeepAliveInterval = TimeSpan.FromSeconds(30),
});

app.UseAuthentication();

app.UseAuthorization();

await app.RunAsync();

namespace Web.Api
{
  public partial class Program;
}
