namespace SafeRide.Application.Features.Auth;

public sealed class AuthException : Exception
{
    public AuthException(
        string code,
        string message,
        int statusCode,
        int? retryAfterSeconds = null)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        RetryAfterSeconds = retryAfterSeconds;
    }

    public string Code { get; }
    public int StatusCode { get; }
    public int? RetryAfterSeconds { get; }
}
