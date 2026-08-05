namespace SafeRide.Application.Features.TripSharing;

public sealed class TripSharingException : Exception
{
    public TripSharingException(string code, string message, int statusCode)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }
    public int StatusCode { get; }
}
