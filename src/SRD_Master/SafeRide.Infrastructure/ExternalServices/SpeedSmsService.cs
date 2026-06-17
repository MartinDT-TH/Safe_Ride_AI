using Microsoft.Extensions.Configuration;
using SafeRide.Application.Features.Auth.Services;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SafeRide.Infrastructure.ExternalServices;

public sealed class InfobipOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.infobip.com";
    public string From { get; set; } = "SafeRide";
}

public sealed class InfobipSmsService : ISpeedSmsService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public InfobipSmsService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task SendOtpAsync(string phoneNumber, string otpCode)
    {
        var options = _configuration.GetSection("Infobip").Get<InfobipOptions>()
            ?? new InfobipOptions();
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException("Infobip ApiKey chưa được cấu hình.");
        }

        var payload = new
        {
            messages = new[]
            {
                new
                {
                    from = string.IsNullOrWhiteSpace(options.From) ? "SafeRide" : options.From,
                    destinations = new[] { new { to = phoneNumber } },
                    text = $"Mã OTP của bạn là {otpCode}. Mã có hiệu lực trong 5 phút."
                }
            }
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{options.BaseUrl.TrimEnd('/')}/sms/2/text/advanced");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("App", options.ApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Infobip gửi OTP thất bại. Status: {(int)response.StatusCode}. Chi tiết: {body}");
        }

        var result = JsonSerializer.Deserialize<InfobipSendResponse>(
            body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (result?.Messages == null || result.Messages.Count == 0)
        {
            throw new InvalidOperationException(
                "Infobip không trả về thông tin tin nhắn.");
        }

        var failedMessage = result.Messages.FirstOrDefault(x => IsFailedStatus(x.Status));
        if (failedMessage != null)
        {
            throw new InvalidOperationException(
                $"Infobip từ chối gửi OTP: {JsonSerializer.Serialize(failedMessage.Status)}");
        }
    }

    private static bool IsFailedStatus(InfobipStatus? status)
    {
        if (status == null)
        {
            return true;
        }

        var groupName = status.GroupName?.Trim() ?? string.Empty;
        return groupName.Contains("REJECTED", StringComparison.OrdinalIgnoreCase)
            || groupName.Contains("FAILED", StringComparison.OrdinalIgnoreCase)
            || groupName.Contains("ERROR", StringComparison.OrdinalIgnoreCase)
            || groupName.Contains("INVALID", StringComparison.OrdinalIgnoreCase)
            || status.GroupId == 0;
    }

    private sealed class InfobipSendResponse
    {
        public List<InfobipMessage>? Messages { get; set; }
    }

    private sealed class InfobipMessage
    {
        public InfobipStatus? Status { get; set; }
    }

    private sealed class InfobipStatus
    {
        public int GroupId { get; set; }
        public string? GroupName { get; set; }
        public string? Name { get; set; }
    }
}