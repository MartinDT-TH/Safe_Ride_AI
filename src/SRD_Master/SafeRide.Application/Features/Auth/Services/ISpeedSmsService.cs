namespace SafeRide.Application.Features.Auth.Services;

public interface ISpeedSmsService
{
    Task SendOtpAsync(string phoneNumber, string otpCode);
}
