using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Infrastructure.Services;

public sealed class PiiProtectionService(IDataProtectionProvider provider) : IPiiProtectionService
{
    private readonly IDataProtector _protector = provider.CreateProtector("SafeRide.DriverKyc.Pii.v1");

    public string Protect(string value) => _protector.Protect(value);

    public string? Unprotect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        try { return _protector.Unprotect(value); }
        catch (CryptographicException) { return value; }
    }
}
