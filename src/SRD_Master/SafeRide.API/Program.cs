using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using SafeRide.Application;
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

// TODO: REMOVE BEFORE FINAL — dev-only provider override.
// Change the value below to switch map provider without editing appsettings.
// "VietMap" | "GoogleMaps" | "OpenRouteService"
// const string devMapProvider = "OpenRouteService";
// builder.Configuration["MapServices:PrimaryProvider"] = devMapProvider;


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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();


    // var runDriverSimulator = builder.Configuration.GetValue<bool>("Simulator:RunDriverLocationSimulator");
    // if (runDriverSimulator)
    // {
    //     // V1: Redis Direct Simulator
    //     Console.WriteLine("\n=== Running V1: Redis Direct Simulator ===");
    //     Console.WriteLine("Starting Driver Location Simulator...");
    //     // await app.Services.GetRequiredService<DriverLocationSimulator>().RunAsync();
    //     _ = Task.Run(async () =>
    //     {
    //         using var scope = app.Services.CreateScope();
    //         await scope.ServiceProvider
    //             .GetRequiredService<DriverLocationSimulator>()
    //             .RunAsync();
    //     });
    // }


    // V2: SignalR Real-time Simulator (if needed, uncomment and configure)
    // Console.WriteLine("\n=== Running V2: SignalR Real-time Simulator ===");
    // await DriverLocationSimulatorV2.Main(Array.Empty<string>());

    // V3: DI-based SignalR Simulator (if needed,    // V3: DI-based SignalR Simulator (Disabled)
    // Console.WriteLine("\n=== Running V3: DI-based SignalR Simulator ===");
    // _ = Task.Run(async () =>
    // {
    //     try
    //     {
    //         // Wait for server to be fully ready
    //         await Task.Delay(10000);
    //         using var scope = app.Services.CreateScope();
    //         var v3Simulator = scope.ServiceProvider.GetRequiredService<DriverLocationSimulatorV3>();
    //         await v3Simulator.StartAsync("0901000002", 10);
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.WriteLine($"Simulator Error: {ex.Message}");
    //     }
    // });
}

app.UseMiddleware<ApiExceptionMiddleware>();
app.UseHttpsRedirection();
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

await app.Services.SeedIdentityAsync();
app.MapControllers();
app.MapHub<SafeRideHub>("/hubs/saferide");
app.Run();

public partial class Program;
