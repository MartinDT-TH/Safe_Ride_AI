namespace SafeRide.Application.Features.Safety;

public sealed class SafetyException : Exception
{
    public SafetyException(string code, string message, int statusCode)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }
    public int StatusCode { get; }
}
