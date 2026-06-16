using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using SafeRide.Application;
using SafeRide.API.Middlewares;
using SafeRide.Infrastructure;
using SafeRide.Infrastructure.Persistence;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile(
        "appsettings.Local.json",
        optional: true,
        reloadOnChange: true);
}

const string mapRoutingProvider = "OpenRouteService"; // Use "OpenRouteService" to switch provider. Google
builder.Configuration["MapRouting:Provider"] = mapRoutingProvider;

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
}

app.UseMiddleware<ApiExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseMiddleware<AuthRateLimitMiddleware>();
app.UseAuthentication();
app.UseMiddleware<SafeRide.API.Middlewares.ProfileCompletionMiddleware>();
app.UseAuthorization();

await app.Services.SeedIdentityAsync();
app.MapControllers();
app.Run();

public partial class Program;
