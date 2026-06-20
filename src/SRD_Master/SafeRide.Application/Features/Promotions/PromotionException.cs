namespace SafeRide.Application.Features.Promotions;

public sealed class PromotionException : Exception
{
    public PromotionException(string code, string message, int statusCode)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }
    public int StatusCode { get; }
}
