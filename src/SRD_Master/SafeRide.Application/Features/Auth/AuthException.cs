namespace SafeRide.Application.Features.Auth;

public sealed class AuthException : Exception
{
    public AuthException(string code, string message, int statusCode)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }
    public int StatusCode { get; }
}