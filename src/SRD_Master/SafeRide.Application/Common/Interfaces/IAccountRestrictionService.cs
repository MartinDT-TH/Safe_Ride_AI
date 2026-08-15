using SafeRide.Application.Features.AccountBans.DTOs;

namespace SafeRide.Application.Common.Interfaces;

public interface IAccountRestrictionService
{
    Task<AccountRestrictionCheckResult> CheckAccountAccessAsync(
        Guid userId,
        bool releaseExpiredTemporaryBans,
        CancellationToken cancellationToken);

    Task RecordManualLockAsync(
        Guid userId,
        Guid adminUserId,
        string reason,
        CancellationToken cancellationToken);

    Task RecordManualUnlockAsync(
        Guid userId,
        Guid adminUserId,
        CancellationToken cancellationToken);
}
