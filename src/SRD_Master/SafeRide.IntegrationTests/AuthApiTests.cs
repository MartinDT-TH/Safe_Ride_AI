using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SafeRide.Application.Features.Auth.DTOs;
using SafeRide.Application.Features.Auth.Services;
using SafeRide.Domain.Entities;
using SafeRide.Infrastructure.Persistence;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SafeRide.IntegrationTests;

public sealed class AuthApiTests
{
    [Fact]
    public async Task GoogleLogin_CreatesCustomerAndIssuesTokens()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/google-login",
            new GoogleLoginRequest
            {
                GoogleIdToken = "valid-google-token",
                DeviceId = "google-device",
                DeviceName = "Integration Test"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(login);
        Assert.Equal("google@example.test", login.Email);
        Assert.Contains("Customer", login.Roles);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(login.RefreshToken));

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AspNetUser>>();
        var user = await userManager.FindByLoginAsync("Google", "google-subject");
        Assert.NotNull(user);
        Assert.True(user.EmailConfirmed);
    }

    [Fact]
    public async Task GoogleLogin_LinksExistingAccountByVerifiedEmail()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        Guid existingUserId;

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AspNetUser>>();
            var user = new AspNetUser
            {
                Id = Guid.NewGuid(),
                UserName = "+84901234599",
                PhoneNumber = "+84901234599",
                Email = "google@example.test",
                FullName = "Existing User",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            Assert.True((await userManager.CreateAsync(user)).Succeeded);
            existingUserId = user.Id;
        }

        var response = await client.PostAsJsonAsync(
            "/api/auth/google-login",
            new GoogleLoginRequest { GoogleIdToken = "valid-google-token" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(login);
        Assert.Equal(existingUserId, login.UserId);

        using var verificationScope = factory.Services.CreateScope();
        var verificationManager = verificationScope.ServiceProvider
            .GetRequiredService<UserManager<AspNetUser>>();
        var linked = await verificationManager.FindByLoginAsync(
            "Google",
            "google-subject");
        Assert.Equal(existingUserId, linked?.Id);
    }

    [Fact]
    public async Task OtpLogin_Refresh_Me_AndReplay_FollowExpectedContract()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var login = await LoginAsync(factory, client, "0901234567");
        Assert.Equal("+84901234567", login.PhoneNumber);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);

        var refreshResponse = await client.PostAsJsonAsync(
            "/api/auth/refresh-token",
            new RefreshTokenRequest
            {
                RefreshToken = login.RefreshToken,
                DeviceId = "integration-device"
            });
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var rotated = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(rotated);
        Assert.NotEqual(login.RefreshToken, rotated.RefreshToken);

        var replay = await client.PostAsJsonAsync(
            "/api/auth/refresh-token",
            new RefreshTokenRequest { RefreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal("auth.refresh_token_reused", await ReadProblemCodeAsync(replay));

        var revokedFamily = await client.PostAsJsonAsync(
            "/api/auth/refresh-token",
            new RefreshTokenRequest { RefreshToken = rotated.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, revokedFamily.StatusCode);
    }

    [Fact]
    public async Task Otp_IsSingleUse()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        const string phone = "0901234568";

        var code = await SendOtpAndGetCodeAsync(factory, client, phone);
        var first = await VerifyOtpAsync(client, phone, code);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var replay = await VerifyOtpAsync(client, phone, code);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal("auth.otp_expired", await ReadProblemCodeAsync(replay));
    }

    [Fact]
    public async Task Otp_FifthInvalidAttempt_BlocksAndConsumesCode()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        const string phone = "0901234569";

        var validCode = await SendOtpAndGetCodeAsync(factory, client, phone);
        HttpResponseMessage? response = null;
        for (var index = 0; index < 5; index++)
        {
            response = await VerifyOtpAsync(client, phone, "000000");
        }

        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("auth.otp_attempts_exceeded", await ReadProblemCodeAsync(response));

        var validAfterBlock = await VerifyOtpAsync(client, phone, validCode);
        Assert.Equal(HttpStatusCode.Unauthorized, validAfterBlock.StatusCode);
    }

    [Fact]
    public async Task InactiveUser_IsRejected()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        const string phone = "0901234570";
        await LoginAsync(factory, client, phone);

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AspNetUser>>();
            var user = userManager.Users.Single(x => x.PhoneNumber == "+84901234570");
            user.IsActive = false;
            Assert.True((await userManager.UpdateAsync(user)).Succeeded);
        }

        var code = await SendOtpAndGetCodeAsync(factory, client, phone);
        var response = await VerifyOtpAsync(client, phone, code);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("auth.account_inactive", await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task Validation_DemoLogin_AndSendOtpRateLimit_AreEnforced()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var validation = await client.PostAsJsonAsync(
            "/api/auth/verify-otp",
            new VerifyOtpRequest());
        Assert.Equal(HttpStatusCode.BadRequest, validation.StatusCode);
        Assert.Equal("request.validation_failed", await ReadProblemCodeAsync(validation));

        var demo = await client.PostAsJsonAsync(
            "/api/auth/demo-login",
            new { provider = "Google", email = "demo@example.test" });
        Assert.Equal(HttpStatusCode.NotFound, demo.StatusCode);

        HttpResponseMessage? response = null;
        for (var index = 0; index < 4; index++)
        {
            response = await client.PostAsJsonAsync(
                "/api/auth/send-otp",
                new SendOtpRequest { PhoneNumber = "0901234571" });
        }

        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.True(response.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task ConcurrentRefresh_AllowsOnlyOneRotation()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        var login = await LoginAsync(factory, client, "0901234572");
        var request = new RefreshTokenRequest { RefreshToken = login.RefreshToken };

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/auth/refresh-token", request),
            client.PostAsJsonAsync("/api/auth/refresh-token", request));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_RevokesCurrentSession()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        var login = await LoginAsync(factory, client, "0901234573");

        var logout = await client.PostAsJsonAsync(
            "/api/auth/logout",
            new LogoutRequest { RefreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);

        var refresh = await client.PostAsJsonAsync(
            "/api/auth/refresh-token",
            new RefreshTokenRequest { RefreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task ExpiredRefreshToken_IsRejected()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        var login = await LoginAsync(factory, client, "0901234574");

        using (var scope = factory.Services.CreateScope())
        {
            var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hash = tokenService.HashToken(login.RefreshToken);
            var token = dbContext.RefreshTokens.Single(x => x.TokenHash == hash);
            token.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await dbContext.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync(
            "/api/auth/refresh-token",
            new RefreshTokenRequest { RefreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("auth.refresh_token_expired", await ReadProblemCodeAsync(response));
    }

    private static async Task<AuthResponse> LoginAsync(
        AuthApiFactory factory,
        HttpClient client,
        string phone)
    {
        var code = await SendOtpAndGetCodeAsync(factory, client, phone);
        var response = await VerifyOtpAsync(client, phone, code);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private static async Task<string> SendOtpAndGetCodeAsync(
        AuthApiFactory factory,
        HttpClient client,
        string phone)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/send-otp",
            new SendOtpRequest { PhoneNumber = phone });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var normalized = phone.StartsWith('0') ? $"+84{phone[1..]}" : phone;
        var sms = factory.Services.GetRequiredService<FakeSmsService>();
        Assert.True(sms.Codes.TryGetValue(normalized, out var code));
        return code;
    }

    private static Task<HttpResponseMessage> VerifyOtpAsync(
        HttpClient client,
        string phone,
        string code)
    {
        return client.PostAsJsonAsync(
            "/api/auth/verify-otp",
            new VerifyOtpRequest
            {
                PhoneNumber = phone,
                OtpCode = code,
                DeviceId = "integration-device",
                DeviceName = "Integration Test"
            });
    }

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out var code)
            ? code.GetString()
            : null;
    }
}
