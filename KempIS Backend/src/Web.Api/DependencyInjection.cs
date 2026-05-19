using Web.Api.Infrastructure;
using Web.Api.Realtime;

namespace Web.Api;

public static class DependencyInjection
{
  public static IServiceCollection AddPresentation(this IServiceCollection services)
  {
    services.AddExceptionHandler<GlobalExceptionHandler>();
    services.AddProblemDetails();
    services.AddScoped<ReceptionRealtimeSession>();

    services.ConfigureHttpJsonOptions(options =>
    {
      options.SerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
      options.SerializerOptions.Converters.Add(new NullableUtcDateTimeJsonConverter());
    });

    return services;
  }
}
