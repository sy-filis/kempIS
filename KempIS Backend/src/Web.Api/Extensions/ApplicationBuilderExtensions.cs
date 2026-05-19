using Scalar.AspNetCore;

namespace Web.Api.Extensions;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public static class ApplicationBuilderExtensions
{
  public static WebApplication UseOpenApiWithScalar(this WebApplication app)
  {
    app.MapOpenApi();
    app.MapScalarApiReference();

    return app;
  }
}
