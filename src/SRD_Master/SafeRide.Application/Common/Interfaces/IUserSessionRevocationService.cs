namespace SafeRide.Application.Common.Interfaces;

public interface IUserSessionRevocationService
{
    Task RevokeAllUserSessionsAsync(
        Guid userId,
        string reason,
        CancellationToken cancellationToken);
}
