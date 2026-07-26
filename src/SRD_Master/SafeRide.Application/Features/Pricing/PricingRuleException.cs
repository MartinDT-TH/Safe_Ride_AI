namespace SafeRide.Application.Features.Pricing;

public sealed class PricingRuleException : Exception
{
    public PricingRuleException(string code, string message, int statusCode)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }
    public int StatusCode { get; }
}
