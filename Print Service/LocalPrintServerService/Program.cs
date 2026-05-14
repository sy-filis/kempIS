using LocalPrintServerService;
using LocalPrintServerService.Options;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

WebApplicationBuilder builder = CreateBuilder(args);
string sumatraPath = ResolveSumatraPath(builder.Configuration);

ConfigureServices(builder.Services, builder.Configuration, sumatraPath);

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
        source: "KempISPrintingService",
        logName: "Application",
        manageEventSource: false,
        restrictedToMinimumLevel: LogEventLevel.Warning,
        outputTemplate: "[{Level:u3}] {SourceContext:l}  {Message:lj}{NewLine}{Exception}");
  }
});

WebApplication app = builder.Build();

LogMissingSumatraWarning(app, sumatraPath);
app.UseCors(CorsConfiguration.CorsPolicyName);
MapEndpoints(app);

await app.RunAsync();

return;

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
    options.Limits.MaxRequestBodySize = 20L * 1024 * 1024;
  });

  return builder;
}

static void ConfigureServices(IServiceCollection services, IConfiguration configuration, string sumatraPath)
{
  // No-op when run as a console app; activates SCM integration under services.exe.
  services.AddWindowsService();

  services.Configure<ApiDocumentationOptions>(configuration.GetSection(ApiDocumentationOptions.SectionName));

  services.AddSingleton<IPrinterSpooler>(_ => new WindowsPrinterSpooler(sumatraPath));

  services.AddConfiguredCors(configuration);

  services.AddOpenApi(options =>
  {
    options.AddOperationTransformer<ResponseExamplesTransformer>();
  });
}

static void LogMissingSumatraWarning(WebApplication app, string sumatraPath)
{
  // Log the missing-SumatraPDF condition at startup rather than at first
  // print call, so the operator sees the problem in the service log as
  // soon as the service starts instead of only when a user hits the API.
  if (!File.Exists(sumatraPath))
  {
    app.Logger.LogWarning(
        "SumatraPDF not found at {path}. POST /printers/{{name}} will fail until you place SumatraPDF.exe there. Download: https://www.sumatrapdfreader.org/download-free-pdf-viewer",
        sumatraPath);
  }
}

static void MapEndpoints(WebApplication app)
{
  app.MapPrintersEndpoints();

  ApiDocumentationOptions docOptions = app.Services.GetRequiredService<IOptions<ApiDocumentationOptions>>().Value;
  if (!docOptions.Enabled)
  {
    return;
  }

  app.MapOpenApi();
  app.MapScalarApiReference();
}

static string ResolveSumatraPath(IConfiguration config)
{
  string? configured = config["Sumatra:Path"];
  return string.IsNullOrWhiteSpace(configured)
      ? Path.Combine(AppContext.BaseDirectory, "SumatraPDF.exe")
      : configured;
}

public partial class Program { }
