namespace SafeRide.Application.Features.AdminUserAccounts;

public sealed class AdminUserAccountException : Exception
{
    public AdminUserAccountException(string code, string message, int statusCode)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }
    public int StatusCode { get; }
}
