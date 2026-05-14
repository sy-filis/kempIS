using KempISGatesService.Data;
using KempISGatesService.Endpoints;
using KempISGatesService.Infrastructure;
using KempISGatesService.Options;
using KempISGatesService.Services;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

WebApplicationBuilder builder = CreateBuilder(args);

ConfigureServices(builder.Services, builder.Configuration);

builder.Host.UseWindowsService();
builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
  loggerConfiguration
      .ReadFrom.Configuration(context.Configuration)
      .ReadFrom.Services(services);

  // EventLog source is created by Install-Service.ps1; skip the sink off-SCM so dev runs don't need it.
  if (WindowsServiceHelpers.IsWindowsService())
  {
    loggerConfiguration.WriteTo.EventLog(
        source: "KempISGatesService",
        logName: "Application",
        manageEventSource: false,
        restrictedToMinimumLevel: LogEventLevel.Warning,
        outputTemplate: "[{Level:u3}] {SourceContext:l}  {Message:lj}{NewLine}{Exception}");
  }
});

WebApplication app = builder.Build();
app.UseCors(CorsConfiguration.CorsPolicyName);
MapEndpoints(app);

// Open and immediately dispose a connection to each MDB so an unreachable database (missing
// file, share offline, sustained lock) surfaces before the listener accepts traffic.
DatabaseOptions databaseOptions = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
ILogger<Program> startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
ICardRepository cardRepository = app.Services.GetRequiredService<ICardRepository>();
IEventRepository eventRepository = app.Services.GetRequiredService<IEventRepository>();
OleDbRetry.Execute(cardRepository.Probe, databaseOptions.RetryCount, startupLogger, "Users database probe");
OleDbRetry.Execute(eventRepository.Probe, databaseOptions.RetryCount, startupLogger, "Events database probe");

AppLifecycleEventLogger lifecycleLogger = app.Services.GetRequiredService<AppLifecycleEventLogger>();
lifecycleLogger.LogProgramBegin();
app.Lifetime.ApplicationStopping.Register(lifecycleLogger.LogProgramEnd);

await app.RunAsync();

static WebApplicationBuilder CreateBuilder(string[] args)
{
  // SCM launches services with CWD=C:\Windows\System32; anchor it so config & log paths resolve next to the exe.
  Directory.SetCurrentDirectory(AppContext.BaseDirectory);

  WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
  {
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
  });

  builder.WebHost.UseKestrel(options =>
  {
    options.AddServerHeader = false;
  });

  return builder;
}

static void ConfigureServices(IServiceCollection services, ConfigurationManager configuration)
{
  // No-op when run as a console app; activates SCM integration under services.exe.
  services.AddWindowsService();

  services.AddOptions<DatabaseOptions>()
      .Bind(configuration.GetSection(DatabaseOptions.SectionName))
      .Validate(o => o.RetryCount >= 0, "Databases:RetryCount must be >= 0.")
      .ValidateOnStart();
  services.Configure<ApiDocumentationOptions>(configuration.GetSection(ApiDocumentationOptions.SectionName));
  services.AddSingleton<ICardRepository, CardRepository>();
  services.AddSingleton<IEventRepository, EventRepository>();
  services.AddSingleton<CardService>();
  services.AddSingleton<AppLifecycleEventLogger>();

  services.AddConfiguredCors(configuration);

  services.AddOpenApi();
}

static void MapEndpoints(WebApplication app)
{
  app.MapCardEndpoints();

  ApiDocumentationOptions docOptions = app.Services.GetRequiredService<IOptions<ApiDocumentationOptions>>().Value;
  if (!docOptions.Enabled)
  {
    return;
  }

  app.MapOpenApi();
  app.MapScalarApiReference();
}

public partial class Program { }
