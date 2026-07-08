using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace SafeRide.API.Authorization;

public static class TripContinuationAuthorizationExtensions
{
    public static IServiceCollection AddSafeRideContinuationAuthorization(
        this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IAuthorizationHandler, TripContinuationAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder(
                    JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .AddRequirements(new TripContinuationRequirement())
                .Build();
        });

        return services;
    }
}
