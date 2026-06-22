using Microsoft.Extensions.DependencyInjection;
using SafeRide.Application.Common.Interfaces;
using System.Text.Json.Serialization;

namespace SafeRide.Realtime;

public static class DependencyInjection
{
    public static IServiceCollection AddSafeRideRealtime(
        this IServiceCollection services)
    {
        services.AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.Converters.Add(
                    new JsonStringEnumConverter());
            });
        services.AddSingleton<IRealtimeNotificationService, SignalRRealtimeNotificationService>();

        return services;
    }
}
