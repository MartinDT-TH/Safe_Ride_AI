namespace SafeRide.Application.Common.Interfaces;

/// <summary>
/// Reads DriverKyc values created with the former "SafeRide" discriminator
/// before the portable shared key ring was introduced.
/// </summary>
public interface IPreviousDriverKycPiiProtectionService
{
    bool TryUnprotect(string protectedValue, out string? plaintext);
}
