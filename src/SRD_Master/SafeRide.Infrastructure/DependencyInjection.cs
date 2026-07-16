using Hangfire;
using Hangfire.SqlServer;
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
using SafeRide.Infrastructure.ExternalServices.VietMap;
using SafeRide.Infrastructure.ExternalServices.NoOp;
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
            .ValidateDataAnnotations();
            // NOTE: ValidateOnStart removed — Google Maps is a fallback provider.
            // URL validation is skipped when VietMap is the primary provider.
        services
            .AddOptions<OpenRouteServiceOptions>()
            .Bind(configuration.GetSection(OpenRouteServiceOptions.SectionName));
            // NOTE: ValidateOnStart removed — OpenRouteService is a fallback provider.

        services
            .AddOptions<MatchingOptions>()
            .Bind(configuration.GetSection(MatchingOptions.SectionName))
            .Validate(options => options.InitialRadiusKm > 0, "BackgroundJobs:MatchingOptions:InitialRadiusKm must be greater than zero.")
            .Validate(options => options.ExpandedRadiusKm >= options.InitialRadiusKm, "BackgroundJobs:MatchingOptions:ExpandedRadiusKm must be greater than or equal to InitialRadiusKm.")
            .Validate(options => options.ExpandAfterMinutes > 0, "BackgroundJobs:MatchingOptions:ExpandAfterMinutes must be greater than zero.")
            .Validate(options => options.BookingExpireAfterMinutes > options.ExpandAfterMinutes, "BackgroundJobs:MatchingOptions:BookingExpireAfterMinutes must be greater than ExpandAfterMinutes.")
            .Validate(options => options.OfferExpireSeconds > 0, "BackgroundJobs:MatchingOptions:OfferExpireSeconds must be greater than zero.")
            .Validate(options => options.CustomerConfirmExpireSeconds > 0, "BackgroundJobs:MatchingOptions:CustomerConfirmExpireSeconds must be greater than zero.")
            .Validate(options => options.MatchingTickSeconds > 0, "BackgroundJobs:MatchingOptions:MatchingTickSeconds must be greater than zero.")
            .ValidateOnStart();

        services
            .AddOptions<ScheduledBookingMatchingOptions>()
            .Bind(configuration.GetSection(ScheduledBookingMatchingOptions.SectionName))
            .Validate(options => options.StartMatchingBeforeMinutes > 0, "BackgroundJobs:ScheduledBookingMatching:StartMatchingBeforeMinutes must be greater than zero.")
            .Validate(options => options.PollingIntervalSeconds > 0, "BackgroundJobs:ScheduledBookingMatching:PollingIntervalSeconds must be greater than zero.")
            .ValidateOnStart();

        services
            .AddOptions<ExpandSearchingRadiusJobOptions>()
            .Bind(configuration.GetSection(ExpandSearchingRadiusJobOptions.SectionName))
            .Validate(options => options.RadiusExpandedNotificationTtlMinutes > 0, "BackgroundJobs:ExpandSearchingRadius:RadiusExpandedNotificationTtlMinutes must be greater than zero.")
            .ValidateOnStart();

        services
            .AddOptions<CleanupStaleDriverLocationJobOptions>()
            .Bind(configuration.GetSection(CleanupStaleDriverLocationJobOptions.SectionName))
            .Validate(options => options.StaleAfterMinutes > 0, "BackgroundJobs:CleanupStaleDriverLocation:StaleAfterMinutes must be greater than zero.")
            .ValidateOnStart();

        services
            .AddOptions<BookingLifecycleJobSchedulerOptions>()
            .Bind(configuration.GetSection(BookingLifecycleJobSchedulerOptions.SectionName))
            .Validate(options => options.JobIdTtlHours > 0, "BackgroundJobs:BookingLifecycleJobScheduler:JobIdTtlHours must be greater than zero.")
            .ValidateOnStart();

        services
            .AddOptions<SimulatorOptions>()
            .Bind(configuration.GetSection(SimulatorOptions.SectionName))
            .Validate(options => options.MockDriverTtlRefreshSeconds > 0, "SimulatorOptions:MockDriverTtlRefreshSeconds must be greater than zero.")
            .Validate(options => options.MockBookingIntervalSeconds > 0, "SimulatorOptions:MockBookingIntervalSeconds must be greater than zero.")
            .Validate(options => options.MaxConcurrentMockBookings >= 0, "SimulatorOptions:MaxConcurrentMockBookings must be >= 0.")
            .Validate(options => options.MockBookingBaseLat >= -90 && options.MockBookingBaseLat <= 90, "SimulatorOptions:MockBookingBaseLat must be between -90 and 90.")
            .Validate(options => options.MockBookingBaseLng >= -180 && options.MockBookingBaseLng <= 180, "SimulatorOptions:MockBookingBaseLng must be between -180 and 180.")
            .ValidateOnStart();

        // ── Hangfire ───────────────────────────────────────────────────────────────
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(
                configuration.GetConnectionString("DefaultConnection"),
                new SqlServerStorageOptions
                {
                    CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                    SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                    QueuePollInterval = TimeSpan.Zero,
                    UseRecommendedIsolationLevel = true,
                    DisableGlobalLocks = true
                }));
        services.AddHangfireServer();
        services.AddScoped<IBookingLifecycleJobScheduler, HangfireBookingLifecycleJobScheduler>();
        // ──────────────────────────────────────────────────────────────────────────

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
        services.AddScoped<IRatingRepository, RatingRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IMatchingPolicyProvider, MatchingPolicyProvider>();
        services.AddScoped<IBookingMatchingService, BookingMatchingService>();
        services.AddScoped<IBookingAssignmentService, BookingAssignmentService>();
        services.AddScoped<IDriverRealtimeService, DriverRealtimeService>();
        services.AddScoped<ITripStatusService, TripStatusService>();
        services.AddHttpClient<ISpeedSmsService, InfobipSmsService>();

        // ── Map Services ───────────────────────────────────────────────────────────
        // VietMap options (always registered regardless of primary provider)
        services
            .AddOptions<VietMapOptions>()
            .Bind(configuration.GetSection(VietMapOptions.SectionName));

        var primaryMapProvider = configuration["MapServices:PrimaryProvider"];

        if (string.Equals(primaryMapProvider, "OpenRouteService", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IMapRoutingService, OpenRouteServiceRoutingService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(20);
            });
            if(!configuration.GetValue<bool>("MapServices:TurnGeocodingOffForOpenRouteServiceFallback")) 
            {
                services.AddHttpClient<IMapGeocodingService, OpenRouteServiceGeocodingService>(client =>
                {
                    var timeoutSeconds = configuration.GetValue<int>("MapServices:OpenRouteService:TimeoutSeconds", 20);
                    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                });
            }
            else
            {
                services.AddSingleton<IMapGeocodingService, NoOpGeocodingService>();
            }
            
        }
        else if (string.Equals(primaryMapProvider, "GoogleMaps", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IMapRoutingService, GoogleMapsRoutingService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
            });
            // Fallback to VietMap for Geocoding until GoogleMapsGeocodingService is implemented
            services.AddHttpClient<IMapGeocodingService, VietMapGeocodingService>(client =>
            {
                var timeoutSeconds = configuration.GetValue<int>("MapServices:VietMap:TimeoutSeconds", 15);
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            });
        }
        else
        {
            // Default: VietMap
            services.AddHttpClient<IMapRoutingService, VietMapRoutingService>(client =>
            {
                var timeoutSeconds = configuration.GetValue<int>("MapServices:VietMap:TimeoutSeconds", 15);
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            });
            services.AddHttpClient<IMapGeocodingService, VietMapGeocodingService>(client =>
            {
                var timeoutSeconds = configuration.GetValue<int>("MapServices:VietMap:TimeoutSeconds", 15);
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            });
        }
        // ──────────────────────────────────────────────────────────────────────────

        if (environment.IsDevelopment())
        {
            if (configuration.GetValue<bool>("Simulator:EnableMockDrivers"))
            {
                services.AddHostedService<MockDriverOfferAcceptorService>();
            }

            if (configuration.GetValue<bool>("Simulator:EnableMockCustomerService"))
            {
                services.AddHostedService<MockCustomerSimulatorService>();
            }

            if (configuration.GetValue<bool>("Simulator:EnableMockBookingGenerator"))
            {
                services.AddHostedService<MockBookingGeneratorService>();
            }
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
