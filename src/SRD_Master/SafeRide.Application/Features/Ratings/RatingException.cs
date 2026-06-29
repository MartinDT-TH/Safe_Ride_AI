namespace SafeRide.Application.Features.Ratings;

public sealed class RatingException : Exception
{
    public RatingException(string code, string message, int statusCode)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }
    public int StatusCode { get; }
}
