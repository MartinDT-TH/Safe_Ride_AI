namespace SafeRide.Application.Features.Reports;

public sealed class ReportException : Exception
{
    public ReportException(string code, string message, int statusCode)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }
    public int StatusCode { get; }
}
