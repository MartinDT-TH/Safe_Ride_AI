namespace SafeRide.Application.Common.Interfaces;

/// <summary>
/// Reads DriverKyc values protected before the application discriminator was
/// explicitly set to "SafeRide". This is read-only compatibility support.
/// </summary>
public interface ILegacyDriverKycPiiProtectionService
{
    bool TryUnprotect(string protectedValue, out string? plaintext);
}
