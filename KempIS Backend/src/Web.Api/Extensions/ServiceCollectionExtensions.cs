using Web.Api.Infrastructure;
using Web.Api.OpenApi;

namespace Web.Api.Extensions;

internal static class ServiceCollectionExtensions
{
  internal const string CorsPolicyName = "Default";

  internal static IServiceCollection AddOpenApiWithAuth(this IServiceCollection services)
  {
    services.AddOpenApi(options =>
    {
      options.AddDocumentTransformer<KempISDocumentTransformer>();
      options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
      options.AddOperationTransformer<BearerSecurityOperationTransformer>();
    });

    return services;
  }

  internal static IServiceCollection AddConfiguredCors(this IServiceCollection services, IConfiguration configuration)
  {
    IConfigurationSection section = configuration.GetSection(CorsPolicyOptions.SectionName);
    services.Configure<CorsPolicyOptions>(section);

    CorsPolicyOptions options = section.Get<CorsPolicyOptions>() ?? new CorsPolicyOptions();

    services.AddCors(o => o.AddPolicy(CorsPolicyName, policy =>
    {
      if (options.AllowedOrigins.Contains("*"))
      {
        policy.AllowAnyOrigin();
      }
      else if (options.AllowedOrigins.Length > 0)
      {
        policy.WithOrigins(options.AllowedOrigins);
      }

      if (options.AllowedMethods.Contains("*") || options.AllowedMethods.Length == 0)
      {
        policy.AllowAnyMethod();
      }
      else
      {
        policy.WithMethods(options.AllowedMethods);
      }

      if (options.AllowedHeaders.Contains("*") || options.AllowedHeaders.Length == 0)
      {
        policy.AllowAnyHeader();
      }
      else
      {
        policy.WithHeaders(options.AllowedHeaders);
      }

      if (options.ExposedHeaders.Length > 0)
      {
        policy.WithExposedHeaders(options.ExposedHeaders);
      }

      if (options.AllowCredentials && !options.AllowedOrigins.Contains("*"))
      {
        policy.AllowCredentials();
      }
    }));

    return services;
  }
}
