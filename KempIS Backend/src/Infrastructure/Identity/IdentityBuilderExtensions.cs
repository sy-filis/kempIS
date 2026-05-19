using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Infrastructure.Identity;

public static class IdentityBuilderExtensions
{
  public static IdentityBuilder AddPasskeys(this IdentityBuilder builder)
  {
    builder.Services.TryAddScoped(typeof(IPasskeyHandler<>), typeof(PasskeyHandler<>));
    return builder;
  }
}
