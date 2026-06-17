using Microsoft.Extensions.DependencyInjection;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Realtime;

public static class DependencyInjection
{
    public static IServiceCollection AddSafeRideRealtime(
        this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddSingleton<IRealtimeNotificationService, SignalRRealtimeNotificationService>();

        return services;
    }
}
