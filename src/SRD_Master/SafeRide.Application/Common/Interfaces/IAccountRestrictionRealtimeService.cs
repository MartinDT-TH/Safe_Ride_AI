using SafeRide.Application.Common.Realtime;

namespace SafeRide.Application.Common.Interfaces;

public interface IAccountRestrictionRealtimeService
{
    Task PublishAccountRestrictionAppliedAsync(
        AccountRestrictionAppliedEvent notification,
        CancellationToken cancellationToken = default);
}
