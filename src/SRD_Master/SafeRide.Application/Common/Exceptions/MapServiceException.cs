namespace SafeRide.Application.Common.Exceptions;

public sealed class MapServiceException : Exception
{
    public MapServiceException(string message)
        : base(message)
    {
    }

    public MapServiceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
