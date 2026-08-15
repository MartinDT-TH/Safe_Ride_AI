namespace SafeRide.Application.Features.AccountBans;

public sealed class AccountBanException : Exception
{
    public AccountBanException(string code, string message, int statusCode)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }
    public int StatusCode { get; }
}
