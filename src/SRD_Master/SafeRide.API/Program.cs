using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using SafeRide.API;
using SafeRide.API.Authorization;
using SafeRide.Application;
using SafeRide.API.Filters;
using SafeRide.API.Middlewares;
using SafeRide.Infrastructure;
using SafeRide.Infrastructure.Persistence;
using Microsoft.Extensions.FileProviders;
using SafeRide.Infrastructure.Simulator;
using SafeRide.Realtime;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile(
        "appsettings.Local.json",
        optional: true,
        reloadOnChange: true);
}

var backgroundJobsEnabled = builder.Configuration.GetValue<bool>("BackgroundJobs:Enabled");
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var problem = new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = "Dữ liệu yêu cầu không hợp lệ.",
                Instance = context.HttpContext.Request.Path
            };
            problem.Extensions["code"] = "request.validation_failed";
            problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            return new BadRequestObjectResult(problem);
        };
    });

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddSafeRideContinuationAuthorization();
if (backgroundJobsEnabled)
{
    builder.Services.AddSafeRideApiJobs(builder.Configuration);
}
builder.Services.AddSafeRideRealtime();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

await app.Services.SeedAdminIdentityAsync();
await app.Services.SeedIdentityAsync();
await app.Services.SeedCustomerIdentityAsync();

app.Use(async (context, next) =>
{
    var logger = context.RequestServices
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("RequestLogger");

    logger.LogInformation("Incoming request: {Method} {Path}",
        context.Request.Method,
        context.Request.Path);

    try
    {
        await next();

        logger.LogInformation("Completed request: {Method} {Path} => {StatusCode}",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unhandled exception: {Method} {Path}",
            context.Request.Method,
            context.Request.Path);

        throw;
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    if (!app.Configuration.GetValue<bool>("Simulator:EnableMockDrivers"))
    {
        using var scope = app.Services.CreateScope();
        var redis = scope.ServiceProvider.GetRequiredService<SafeRide.Infrastructure.Redis.IRedisService>();
        var db = scope.ServiceProvider.GetRequiredService<SafeRide.Infrastructure.Persistence.ApplicationDbContext>();
        
        foreach (var mockDriver in SafeRide.Infrastructure.Simulator.MockDriverConfiguration.GetMockDrivers())
        {
            // Remove from Redis Geo cache
            redis.GeoRemoveAsync(SafeRide.Infrastructure.Redis.RedisKeys.OnlineDriversGeo, mockDriver.DriverId.ToString()).GetAwaiter().GetResult();
            redis.RemoveAsync(SafeRide.Infrastructure.Redis.RedisKeys.DriverLocation(mockDriver.DriverId)).GetAwaiter().GetResult();
            redis.RemoveAsync(SafeRide.Infrastructure.Redis.RedisKeys.DriverStatus(mockDriver.DriverId)).GetAwaiter().GetResult();
            redis.RemoveAsync(SafeRide.Infrastructure.Redis.RedisKeys.DriverOnline(mockDriver.DriverId)).GetAwaiter().GetResult();
            
            // Set offline in DB
            var profile = db.DriverProfiles.FirstOrDefault(p => p.DriverId == mockDriver.DriverId);
            if (profile != null)
            {
                profile.WorkStatus = SafeRide.Domain.Enums.DriverWorkStatus.Offline;
            }
        }
        db.SaveChanges();
    }
}

app.UseMiddleware<ApiExceptionMiddleware>();
if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}
var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});
app.UseMiddleware<AuthRateLimitMiddleware>();
app.UseAuthentication();
app.UseMiddleware<ProfileCompletionMiddleware>();
app.UseAuthorization();

// await app.Services.SeedIdentityAsync(app.Lifetime.ApplicationStopping);
// await app.Services.SeedPricingAndSurgeRulesAsync(app.Lifetime.ApplicationStopping);
// await app.Services.SeedBookingFeaturesAsync(app.Lifetime.ApplicationStopping);
app.MapControllers();
app.UseWebSockets();
app.MapHub<SafeRideHub>("/hubs/saferide");

if (backgroundJobsEnabled)
{
    app.UseSafeRideApiJobs();
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization =
        [
            new HangfireAdminAuthorizationFilter()
        ]
    });
}

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "SafeRide.API",
    environment = app.Environment.EnvironmentName,
    timeUtc = DateTime.UtcNow
}));
app.MapGet("/", () => Results.Ok(new
{
    service = "SafeRide.API",
    status = "Running",
    health = "/health",
    swagger = "/swagger/index.html"
}));

app.Run();

public partial class Program;
