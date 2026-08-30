namespace SafeRide.Application.Common.Exceptions;

public sealed class DriverLicenseExpiredException : Exception
{
    public DriverLicenseExpiredException(DateOnly expiryDate)
        : base($"Bằng lái xe của bạn đã hết hạn từ ngày {expiryDate:dd/MM/yyyy}. Vui lòng cập nhật bằng lái mới trước khi online.")
    {
        ExpiryDate = expiryDate;
    }

    public DateOnly ExpiryDate { get; }
}
