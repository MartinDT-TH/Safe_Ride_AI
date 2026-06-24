using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new Exception("SERVER ERROR: " + err);
        }
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(login);
        Assert.Equal("google@example.test", login.Email);
        Assert.Contains("Customer", login.Roles);
        Assert.Equal(AuthNextSteps.CompleteProfile, login.NextStep);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(login.RefreshToken));

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AspNetUser>>();
        var user = await userManager.FindByLoginAsync("Google", "google-subject");
        Assert.NotNull(user);
        Assert.True(user.EmailConfirmed);
    }

    [Fact]
    public async Task GoogleLogin_FirstSignupRequiresPhoneBeforeCustomerHome()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var firstResponse = await client.PostAsJsonAsync(
            "/api/auth/google-login",
            new GoogleLoginRequest { GoogleIdToken = "valid-google-token" });

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstLogin = await firstResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(firstLogin);
        Assert.Equal(AuthNextSteps.CompleteProfile, firstLogin.NextStep);
        Assert.Null(firstLogin.PhoneNumber);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", firstLogin.AccessToken);
        var unverifiedProfileResponse = await client.PutAsJsonAsync(
            "/api/auth/profile",
            new UpdateProfileRequest
            {
                FullName = "Google Customer",
                Email = "google@example.test",
                PhoneNumber = "0901234598"
            });
        Assert.Equal(HttpStatusCode.Conflict, unverifiedProfileResponse.StatusCode);
        Assert.Equal(
            "auth.phone_verification_required",
            await ReadProblemCodeAsync(unverifiedProfileResponse));

        var profilePhoneCode = await SendProfilePhoneOtpAndGetCodeAsync(
            factory,
            client,
            "0901234598");
        var verifyPhoneResponse = await client.PostAsJsonAsync(
            "/api/auth/profile/phone/verify-otp",
            new VerifyOtpRequest
            {
                PhoneNumber = "0901234598",
                OtpCode = profilePhoneCode
            });
        Assert.Equal(HttpStatusCode.OK, verifyPhoneResponse.StatusCode);

        var profileResponse = await client.PutAsJsonAsync(
            "/api/auth/profile",
            new UpdateProfileRequest
            {
                FullName = "Google Customer",
                Email = "google@example.test",
                PhoneNumber = "0901234598"
            });
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var secondResponse = await client.PostAsJsonAsync(
            "/api/auth/google-login",
            new GoogleLoginRequest { GoogleIdToken = "valid-google-token" });

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondLogin = await secondResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(secondLogin);
        Assert.Equal(AuthNextSteps.CustomerHome, secondLogin.NextStep);
        Assert.Equal("+84901234598", secondLogin.PhoneNumber);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AspNetUser>>();
        var user = await userManager.FindByLoginAsync("Google", "google-subject");
        Assert.NotNull(user);
        Assert.True(user.PhoneNumberConfirmed);
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
                PhoneNumberConfirmed = true,
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
        Assert.Equal(AuthNextSteps.CustomerHome, login.NextStep);

        using var verificationScope = factory.Services.CreateScope();
        var verificationManager = verificationScope.ServiceProvider
            .GetRequiredService<UserManager<AspNetUser>>();
        var linked = await verificationManager.FindByLoginAsync(
            "Google",
            "google-subject");
        Assert.Equal(existingUserId, linked?.Id);
    }

    [Fact]
    public async Task LinkedAccounts_CanLinkAndUnlinkGoogleWhenPhoneIsVerified()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var login = await LoginAsync(factory, client, "0901234597");
        await SetFullNameAsync(factory, "+84901234597", "Linked Customer");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var linked = await client.GetFromJsonAsync<LinkedAccountsResponse>(
            "/api/auth/linked-accounts");
        Assert.NotNull(linked);
        Assert.True(linked.PhoneLinked);
        Assert.False(linked.GoogleLinked);

        var linkResponse = await client.PostAsJsonAsync(
            "/api/auth/linked-accounts/google",
            new LinkGoogleAccountRequest { GoogleIdToken = "valid-google-token" });
        Assert.Equal(HttpStatusCode.OK, linkResponse.StatusCode);
        var afterLink = await linkResponse.Content
            .ReadFromJsonAsync<LinkedAccountsResponse>();
        Assert.NotNull(afterLink);
        Assert.True(afterLink.GoogleLinked);
        Assert.Equal("google@example.test", afterLink.GoogleEmail);

        var unlinkResponse = await client.DeleteAsync(
            "/api/auth/linked-accounts/google");
        Assert.Equal(HttpStatusCode.OK, unlinkResponse.StatusCode);
        var afterUnlink = await unlinkResponse.Content
            .ReadFromJsonAsync<LinkedAccountsResponse>();
        Assert.NotNull(afterUnlink);
        Assert.False(afterUnlink.GoogleLinked);
        Assert.Null(afterUnlink.GoogleEmail);
    }

    [Fact]
    public async Task UpdateProfile_EmailChangeRemovesGoogleLogin()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var login = await LoginAsync(factory, client, "0901234598");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var linkResponse = await client.PostAsJsonAsync(
            "/api/auth/linked-accounts/google",
            new LinkGoogleAccountRequest { GoogleIdToken = "valid-google-token" });
        Assert.Equal(HttpStatusCode.OK, linkResponse.StatusCode);

        var profileResponse = await client.PutAsJsonAsync(
            "/api/auth/profile",
            new UpdateProfileRequest
            {
                FullName = "Changed Email Customer",
                PhoneNumber = login.PhoneNumber,
                Email = "manual@example.test"
            });
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AspNetUser>>();
            var oldGoogleLogin = await userManager.FindByLoginAsync(
                "Google",
                "google-subject");
            Assert.Null(oldGoogleLogin);
        }

        client.DefaultRequestHeaders.Authorization = null;
        var googleLoginResponse = await client.PostAsJsonAsync(
            "/api/auth/google-login",
            new GoogleLoginRequest { GoogleIdToken = "valid-google-token" });
        Assert.Equal(HttpStatusCode.OK, googleLoginResponse.StatusCode);
        var googleLogin = await googleLoginResponse.Content
            .ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(googleLogin);
        Assert.NotEqual(login.UserId, googleLogin.UserId);
    }

    [Fact]
    public async Task GoogleLogin_DoesNotModifyInactiveAccount()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        Guid userId;

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AspNetUser>>();
            var user = new AspNetUser
            {
                Id = Guid.NewGuid(),
                UserName = "inactive-google-user",
                Email = "google@example.test",
                EmailConfirmed = false,
                FullName = "Inactive User",
                IsActive = false,
                CreatedAt = DateTime.UtcNow
            };
            Assert.True((await userManager.CreateAsync(user)).Succeeded);
            userId = user.Id;
        }

        var response = await client.PostAsJsonAsync(
            "/api/auth/google-login",
            new GoogleLoginRequest { GoogleIdToken = "valid-google-token" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("auth.account_inactive", await ReadProblemCodeAsync(response));

        using var verificationScope = factory.Services.CreateScope();
        var dbContext = verificationScope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var unchangedUser = dbContext.Users.Single(x => x.Id == userId);
        Assert.False(unchangedUser.EmailConfirmed);
        Assert.Equal("Inactive User", unchangedUser.FullName);
        Assert.Null(unchangedUser.AvatarUrl);
        Assert.Null(unchangedUser.UpdatedAt);
    }

    [Fact]
    public async Task OtpLogin_Refresh_Me_AndReplay_FollowExpectedContract()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var login = await LoginAsync(factory, client, "0901234567");
        Assert.Equal("+84901234567", login.PhoneNumber);

        using (var scope = factory.Services.CreateScope())
        {
            var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tokenHash = tokenService.HashToken(login.RefreshToken);
            var storedToken = dbContext.RefreshTokens.Single(x => x.TokenHash == tokenHash);
            Assert.Equal("integration-device", storedToken.DeviceId);
            Assert.Equal("Integration Test", storedToken.DeviceName);
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var profileResponse = await client.PutAsJsonAsync(
            "/api/auth/profile",
            new UpdateProfileRequest
            {
                FullName = "Restored Customer",
                PhoneNumber = login.PhoneNumber
            });
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);

        var meResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var me = await meResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(me);
        Assert.Equal("+84901234567", me.PhoneNumber);
        Assert.True(me.PhoneNumberConfirmed);
        Assert.Equal("Restored Customer", me.FullName);
        Assert.Equal(AuthNextSteps.CustomerHome, me.NextStep);

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
    public async Task GoogleLogin_RegisteredDriver_MustSelectRole()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AspNetUser>>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = new AspNetUser
            {
                Id = Guid.NewGuid(),
                UserName = "google-driver",
                Email = "google@example.test",
                EmailConfirmed = true,
                FullName = "Google Driver",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            Assert.True((await userManager.CreateAsync(user)).Succeeded);
            dbContext.DriverProfiles.Add(new DriverProfile
            {
                DriverId = user.Id,
                IdentityCardNumber = "079987654321"
            });
            await dbContext.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync(
            "/api/auth/google-login",
            new GoogleLoginRequest { GoogleIdToken = "valid-google-token" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(login);
        Assert.Equal(AuthNextSteps.SelectRole, login.NextStep);
    }

    [Fact]
    public async Task Otp_NewUser_RequiresProfileCompletion()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var login = await LoginAsync(factory, client, "0901234580");

        Assert.Equal(AuthNextSteps.CompleteProfile, login.NextStep);
        Assert.Equal(new[] { "Customer" }, login.Roles);
    }

    [Fact]
    public async Task IncompleteProfile_CannotUseAuthenticatedFeatures()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var login = await LoginAsync(factory, client, "0901234583");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var response = await client.GetAsync("/api/vehicles");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("auth.profile_incomplete", await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task Otp_ExistingCustomer_GoesToCustomerHome()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        const string phone = "0901234581";
        await LoginAsync(factory, client, phone);
        await SetFullNameAsync(factory, "+84901234581", "Existing Customer");

        var secondLogin = await LoginAsync(factory, client, phone);

        Assert.Equal(AuthNextSteps.CustomerHome, secondLogin.NextStep);
        Assert.Equal(new[] { "Customer" }, secondLogin.Roles);
    }

    [Fact]
    public async Task Otp_RegisteredDriver_MustSelectRole()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        const string phone = "0901234582";
        await LoginAsync(factory, client, phone);
        await SetFullNameAsync(factory, "+84901234582", "Registered Driver");

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var driverId = dbContext.AspNetUsers
                .Where(x => x.PhoneNumber == "+84901234582")
                .Select(x => x.Id)
                .Single();
            dbContext.DriverProfiles.Add(new DriverProfile
            {
                DriverId = driverId,
                IdentityCardNumber = "079123456789"
            });
            await dbContext.SaveChangesAsync();
        }

        var secondLogin = await LoginAsync(factory, client, phone);

        Assert.Equal(AuthNextSteps.SelectRole, secondLogin.NextStep);
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
        Assert.Equal(HttpStatusCode.TooManyRequests, validAfterBlock.StatusCode);
        Assert.Equal("auth.otp_attempts_exceeded", await ReadProblemCodeAsync(validAfterBlock));
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
    public async Task RefreshToken_ForInactiveUser_RevokesSession()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        var login = await LoginAsync(factory, client, "0901234575");

        using (var scope = factory.Services.CreateScope())
        {
            var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AspNetUser>>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tokenHash = tokenService.HashToken(login.RefreshToken);
            var refreshToken = dbContext.RefreshTokens
                .Include(x => x.User)
                .Single(x => x.TokenHash == tokenHash);
            refreshToken.User.IsActive = false;
            Assert.True((await userManager.UpdateAsync(refreshToken.User)).Succeeded);
        }

        var response = await client.PostAsJsonAsync(
            "/api/auth/refresh-token",
            new RefreshTokenRequest { RefreshToken = login.RefreshToken });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("auth.account_inactive", await ReadProblemCodeAsync(response));

        using var verificationScope = factory.Services.CreateScope();
        var verificationTokenService = verificationScope.ServiceProvider
            .GetRequiredService<IJwtTokenService>();
        var verificationDbContext = verificationScope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var hash = verificationTokenService.HashToken(login.RefreshToken);
        Assert.NotNull(verificationDbContext.RefreshTokens
            .Single(x => x.TokenHash == hash)
            .RevokedAt);
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

        using var scope = factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tokenHash = tokenService.HashToken(login.RefreshToken);
        Assert.NotNull(dbContext.RefreshTokens
            .Single(x => x.TokenHash == tokenHash)
            .RevokedAt);
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

    private static async Task SetFullNameAsync(
        AuthApiFactory factory,
        string phoneNumber,
        string fullName)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AspNetUser>>();
        var user = userManager.Users.Single(x => x.PhoneNumber == phoneNumber);
        user.FullName = fullName;
        Assert.True((await userManager.UpdateAsync(user)).Succeeded);
    }

    private static async Task<string> SendOtpAndGetCodeAsync(
        AuthApiFactory factory,
        HttpClient client,
        string phone)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/send-otp",
            new SendOtpRequest { PhoneNumber = phone });
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new Exception("SERVER ERROR: " + err);
        }
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var normalized = phone.StartsWith('0') ? $"+84{phone[1..]}" : phone;
        var sms = factory.Services.GetRequiredService<FakeSmsService>();
        Assert.True(sms.Codes.TryGetValue(normalized, out var code));
        return code;
    }

    private static async Task<string> SendProfilePhoneOtpAndGetCodeAsync(
        AuthApiFactory factory,
        HttpClient client,
        string phone)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/profile/phone/send-otp",
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
