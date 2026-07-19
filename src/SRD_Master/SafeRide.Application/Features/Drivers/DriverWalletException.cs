namespace SafeRide.Application.Features.Drivers;

public sealed class DriverWalletException : Exception
{
    public DriverWalletException(string message) : base(message)
    {
    }
}
