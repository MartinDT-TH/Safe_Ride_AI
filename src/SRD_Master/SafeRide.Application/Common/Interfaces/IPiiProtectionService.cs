namespace SafeRide.Application.Common.Interfaces;

public interface IPiiProtectionService
{
    string Protect(string value);
    string? Unprotect(string? value);
}
