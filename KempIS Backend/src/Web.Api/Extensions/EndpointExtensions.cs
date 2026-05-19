using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Web.Api.Endpoints;

namespace Web.Api.Extensions;

public static class EndpointExtensions
{
  public static IServiceCollection AddEndpoints(this IServiceCollection services, Assembly assembly)
  {
    ServiceDescriptor[] serviceDescriptors = assembly
        .DefinedTypes
        .Where(type => type is { IsAbstract: false, IsInterface: false } &&
                       type.IsAssignableTo(typeof(IEndpoint)))
        .Select(type => ServiceDescriptor.Transient(typeof(IEndpoint), type))
        .ToArray();

    services.TryAddEnumerable(serviceDescriptors);

    return services;
  }

  public static IApplicationBuilder MapEndpoints(
      this WebApplication app,
      RouteGroupBuilder? routeGroupBuilder = null)
  {
    IEnumerable<IEndpoint> endpoints = app.Services.GetRequiredService<IEnumerable<IEndpoint>>();

    IEndpointRouteBuilder builder = routeGroupBuilder is null ? app : routeGroupBuilder;

    foreach (IEndpoint endpoint in endpoints)
    {
      endpoint.MapEndpoint(builder);
    }

    return app;
  }

  public static RouteHandlerBuilder HasRole(this RouteHandlerBuilder app, params string[] roles)
  {
    return app
      .RequireAuthorization(policy => policy.RequireRole(roles))
      .ProducesProblem(StatusCodes.Status401Unauthorized)
      .ProducesProblem(StatusCodes.Status403Forbidden);
  }

  public static RouteGroupBuilder HasRole(this RouteGroupBuilder group, params string[] roles)
  {
    return group
      .RequireAuthorization(policy => policy.RequireRole(roles))
      .ProducesProblem(StatusCodes.Status401Unauthorized)
      .ProducesProblem(StatusCodes.Status403Forbidden);
  }
}
