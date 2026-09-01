using Microsoft.AspNetCore.DataProtection;
using SafeRide.Application.Common.Interfaces;
using System.Security.Cryptography;

namespace SafeRide.Infrastructure.Services;

public sealed class LegacyDriverKycPiiProtectionService(IDataProtector protector)
    : ILegacyDriverKycPiiProtectionService
{
    public bool TryUnprotect(string protectedValue, out string? plaintext)
    {
        try
        {
            plaintext = protector.Unprotect(protectedValue);
            return true;
        }
        catch (CryptographicException)
        {
            plaintext = null;
            return false;
        }
    }
}
