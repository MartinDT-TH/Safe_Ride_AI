namespace SafeRide.Application.Features.Notifications;

public sealed class NotificationException : Exception
{
    public NotificationException(string code, string message, int statusCode)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }

    public int StatusCode { get; }
}
