using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SafeRide.Application.Features.Auth.Services;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Domain.Entities;
using SafeRide.Infrastructure.Authentication;
using SafeRide.Infrastructure.BackgroundJobs;
using SafeRide.Infrastructure.ExternalServices;
using SafeRide.Infrastructure.ExternalServices.GoogleMaps;
using SafeRide.Infrastructure.ExternalServices.OpenRouteService;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;
using SafeRide.Infrastructure.Repositories;
using SafeRide.Infrastructure.Services;
using SafeRide.Infrastructure.Simulator;
using System.Text;
using SafeRide.Infrastructure.ExternalServices.Cloudinary;

namespace SafeRide.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddDbContext<ApplicationDbContext>(
            options => options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions.UseNetTopologySuite()));

        services
            .AddIdentity<AspNetUser, AspNetRole>(options =>
            {
                options.User.RequireUniqueEmail = false;
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(x => x.SecretKey != "CHANGE_ME", "Jwt:SecretKey must be configured.")
            .ValidateOnStart();
        services
            .AddOptions<GoogleAuthOptions>()
            .Bind(configuration.GetSection(GoogleAuthOptions.SectionName));
        services
            .AddOptions<CloudinaryOptions>()
            .Bind(configuration.GetSection(CloudinaryOptions.SectionName));
        services
            .AddOptions<GoogleMapsOptions>()
            .Bind(configuration.GetSection(GoogleMapsOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => Uri.IsWellFormedUriString(
                    options.RoutesApiUrl,
                    UriKind.Absolute),
                "GoogleMaps:RoutesApiUrl must be an absolute URL.")
            .Validate(
                options => Uri.IsWellFormedUriString(
                    options.GeocodingApiUrl,
                    UriKind.Absolute),
                "GoogleMaps:GeocodingApiUrl must be an absolute URL.")
            .ValidateOnStart();
        services
            .AddOptions<OpenRouteServiceOptions>()
            .Bind(configuration.GetSection(OpenRouteServiceOptions.SectionName))
            .Validate(
                options => Uri.IsWellFormedUriString(
                    options.DirectionsApiUrl,
                    UriKind.Absolute),
                "OpenRouteService:DirectionsApiUrl must be an absolute URL.")
            .Validate(
                options => Uri.IsWellFormedUriString(
                    options.MatrixApiUrl,
                    UriKind.Absolute),
                "OpenRouteService:MatrixApiUrl must be an absolute URL.")
            .ValidateOnStart();
        services
            .AddOptions<MatchingOptions>()
            .Bind(configuration.GetSection(MatchingOptions.SectionName))
            .Validate(options => options.InitialRadiusKm > 0, "MatchingOptions:InitialRadiusKm must be greater than zero.")
            .Validate(options => options.ExpandedRadiusKm >= options.InitialRadiusKm, "MatchingOptions:ExpandedRadiusKm must be greater than or equal to InitialRadiusKm.")
            .Validate(options => options.ExpandAfterMinutes > 0, "MatchingOptions:ExpandAfterMinutes must be greater than zero.")
            .Validate(options => options.BookingExpireAfterMinutes > options.ExpandAfterMinutes, "MatchingOptions:BookingExpireAfterMinutes must be greater than ExpandAfterMinutes.")
            .Validate(options => options.OfferExpireSeconds > 0, "MatchingOptions:OfferExpireSeconds must be greater than zero.")
            .Validate(options => options.CustomerConfirmExpireSeconds > 0, "MatchingOptions:CustomerConfirmExpireSeconds must be greater than zero.")
            .Validate(options => options.MatchingTickSeconds > 0, "MatchingOptions:MatchingTickSeconds must be greater than zero.")
            .Validate(options => options.MockDriverTtlRefreshSeconds > 0, "MatchingOptions:MockDriverTtlRefreshSeconds must be greater than zero.")
            .ValidateOnStart();
        services.AddSingleton<RedisService>();
        services.AddSingleton<InMemoryRedisService>();
        services.AddSingleton<IRedisService>(provider =>
            new ResilientRedisService(
                provider.GetRequiredService<RedisService>(),
                provider.GetRequiredService<InMemoryRedisService>(),
                provider.GetRequiredService<ILogger<ResilientRedisService>>()));
        services.AddSingleton<ICloudinaryImageService, CloudinaryImageService>();
        services.AddSingleton<IIdentityDocumentStorage, CloudinaryIdentityDocumentStorage>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IGoogleTokenVerifier, GoogleTokenVerifier>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IPromotionRepository, PromotionRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IMatchingPolicyProvider, MatchingPolicyProvider>();
        services.AddScoped<IBookingMatchingService, BookingMatchingService>();
        services.AddScoped<IBookingAssignmentService, BookingAssignmentService>();
        services.AddScoped<IDriverRealtimeService, DriverRealtimeService>();
        services.AddScoped<ITripStatusService, TripStatusService>();
        services.AddHttpClient<ISpeedSmsService, InfobipSmsService>();
        var mapRoutingProvider = configuration["MapRouting:Provider"];
        if (string.Equals(
                mapRoutingProvider,
                "OpenRouteService",
                StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IGoogleMapsService, OpenRouteServiceRoutingService>(
                client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(20);
                });
        }
        else
        {
            services.AddHttpClient<IGoogleMapsService, GoogleMapsService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
            });
        }

        if (environment.IsDevelopment())
        {
            // services.AddSingleton<DriverLocationSimulator>();
            // Register V3 simulator with logger support
            // services.AddSingleton<DriverLocationSimulatorV3>();
            // Register mock driver offer acceptor service
            services.AddHostedService<MockDriverOfferAcceptorService>();
        }

        if (!environment.IsEnvironment("Testing"))
        {
            services.AddHostedService<ScheduledBookingMatchingJob>();
            services.AddHostedService<BookingMatchingBackgroundService>();
        }

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = !environment.IsDevelopment();
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("JwtAuthentication");
                        logger.LogWarning(
                            context.Exception,
                            "JWT authentication failed for {Path}.",
                            context.Request.Path);
                        return Task.CompletedTask;
                    },
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrWhiteSpace(accessToken)
                            && path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/problem+json";
                        var problem = new ProblemDetails
                        {
                            Status = StatusCodes.Status401Unauthorized,
                            Title = "Unauthorized",
                            Detail = "Access token không hợp lệ hoặc đã hết hạn.",
                            Instance = context.Request.Path
                        };
                        problem.Extensions["code"] = "auth.access_token_invalid";
                        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
                        await context.Response.WriteAsJsonAsync(problem);
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/problem+json";
                        var problem = new ProblemDetails
                        {
                            Status = StatusCodes.Status403Forbidden,
                            Title = "Forbidden",
                            Detail = "Bạn không có quyền truy cập tài nguyên này.",
                            Instance = context.Request.Path
                        };
                        problem.Extensions["code"] = "auth.forbidden";
                        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
                        await context.Response.WriteAsJsonAsync(problem);
                    }
                };
            });

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, accessor) =>
            {
                var jwt = accessor.Value;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwt.SecretKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization();
        return services;
    }

}
