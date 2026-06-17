namespace SafeRide.Application.Features.Bookings;

public sealed class BookingException : Exception
{
    public BookingException(string code, string message, int statusCode)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }
    public int StatusCode { get; }
}
