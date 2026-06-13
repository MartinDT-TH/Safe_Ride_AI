using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Features.Auth.DTOs;
using SafeRide.Application.Features.Auth.Services;
using SafeRide.Domain.Entities;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafeRide.Application.Features.Auth;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace SafeRide.Infrastructure.Authentication;

public sealed class AuthService : IAuthService
{
    private const string PhoneLoginProvider = "InfobipOtp";
    private const string GoogleLoginProvider = "Google";
    private const string CustomerRole = "Customer";
    private const int MaxOtpAttempts = 5;

    private readonly UserManager<AspNetUser> _userManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly IGoogleTokenVerifier _googleTokenVerifier;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRedisService _redisService;
    private readonly ISpeedSmsService _smsService;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<AspNetUser> userManager,
        ApplicationDbContext dbContext,
        IJwtTokenService jwtTokenService,
        IRedisService redisService,
        ISpeedSmsService smsService,
        IGoogleTokenVerifier googleTokenVerifier,
        IOptions<JwtOptions> jwtOptions,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _jwtTokenService = jwtTokenService;
        _redisService = redisService;
        _smsService = smsService;
        _googleTokenVerifier = googleTokenVerifier;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    public async Task SendOtpAsync(SendOtpRequest request)
    {
        var phoneNumber = NormalizePhoneNumber(request.PhoneNumber);

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new AuthException(
                AuthErrorCodes.InvalidPhoneNumber,
                "Số điện thoại không hợp lệ.",
                StatusCodes.Status400BadRequest);
        }

        // var otpCode = Random.Shared.Next(100000, 999999).ToString();
        var otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var otpKey = RedisKeys.Otp(phoneNumber);
        var attemptsKey = RedisKeys.OtpAttempts(phoneNumber);

        try
        {
            await _redisService.SetAsync(
                otpKey,
                ComputeOtpHash(phoneNumber, otpCode),
                TimeSpan.FromMinutes(5));
            await _redisService.RemoveAsync(attemptsKey);
            await _smsService.SendOtpAsync(phoneNumber, otpCode);
        }
        catch (Exception exception)
        {
            await TryRemoveCacheAsync(otpKey);
            await TryRemoveCacheAsync(attemptsKey);
            _logger.LogError(exception, "Could not send OTP to {PhoneNumber}.", phoneNumber);
            throw new AuthException(
                AuthErrorCodes.OtpUnavailable,
                "Dịch vụ OTP tạm thời không khả dụng.",
                StatusCodes.Status503ServiceUnavailable);
        }
    }

    public async Task<AuthResponse> VerifyOtpAsync(
        VerifyOtpRequest request,
        string? ipAddress,
        string? userAgent)
    {
        var phoneNumber = NormalizePhoneNumber(request.PhoneNumber);
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new AuthException(
                AuthErrorCodes.InvalidPhoneNumber,
                "Số điện thoại không hợp lệ.",
                StatusCodes.Status400BadRequest);
        }

        OtpVerificationResult verification;
        try
        {
            verification = await _redisService.VerifyAndConsumeOtpAsync(
                RedisKeys.Otp(phoneNumber),
                RedisKeys.OtpAttempts(phoneNumber),
                ComputeOtpHash(phoneNumber, request.OtpCode),
                MaxOtpAttempts);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Redis unavailable while verifying OTP.");
            throw new AuthException(
                AuthErrorCodes.OtpUnavailable,
                "Dịch vụ OTP tạm thời không khả dụng.",
                StatusCodes.Status503ServiceUnavailable);
        }

        switch (verification)
        {
            case OtpVerificationResult.Missing:
                throw new AuthException(
                    AuthErrorCodes.OtpExpired,
                    "Mã OTP không tồn tại hoặc đã hết hạn.",
                    StatusCodes.Status401Unauthorized);
            case OtpVerificationResult.Invalid:
                throw new AuthException(
                    AuthErrorCodes.InvalidOtp,
                    "Mã OTP không chính xác.",
                    StatusCodes.Status401Unauthorized);
            case OtpVerificationResult.AttemptsExceeded:
                throw new AuthException(
                    AuthErrorCodes.OtpAttemptsExceeded,
                    "Bạn đã nhập sai OTP quá nhiều lần. Vui lòng yêu cầu mã mới.",
                    StatusCodes.Status429TooManyRequests);
        }

        var (user, isNewUser) = await FindOrCreatePhoneUserAsync(phoneNumber);
        EnsureUserActive(user);
        await EnsurePhoneLoginAsync(user, phoneNumber);
        await EnsureCustomerRoleAsync(user);
        return await GenerateTokenResponseAsync(
            user,
            request.DeviceId,
            request.DeviceName,
            isNewUser);
    }


    public async Task<AuthResponse> GoogleLoginAsync(
        GoogleLoginRequest request,
        string? ipAddress,
        string? userAgent)
    {
        var googleUser = await _googleTokenVerifier.VerifyAsync(request.GoogleIdToken);
        var user = await _userManager.FindByLoginAsync(
            GoogleLoginProvider,
            googleUser.Subject);

        if (user == null)
        {
            user = await FindOrCreateGoogleUserAsync(googleUser);
            await EnsureGoogleLoginAsync(user, googleUser.Subject);
        }

        EnsureUserActive(user);
        await EnsureCustomerRoleAsync(user);
        return await GenerateTokenResponseAsync(user, request.DeviceId, request.DeviceName);
    }

    public async Task<AuthResponse> RefreshTokenAsync(
        RefreshTokenRequest request,
        string? ipAddress,
        string? userAgent)
    {
        var tokenHash = _jwtTokenService.HashToken(request.RefreshToken);
        var oldCacheKey = RedisKeys.RefreshToken(Convert.ToHexString(tokenHash));

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable);

        var refreshToken = await _dbContext.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash);

        if (refreshToken == null)
        {
            throw new AuthException(
                AuthErrorCodes.InvalidRefreshToken,
                "Phiên đăng nhập không hợp lệ.",
                StatusCodes.Status401Unauthorized);
        }

        if (refreshToken.RevokedAt != null)
        {
            await RevokeSessionTokensAsync(refreshToken.SessionId);
            await transaction.CommitAsync();
            await TryRemoveCacheAsync(oldCacheKey);
            throw new AuthException(
                AuthErrorCodes.RefreshTokenReused,
                "Refresh token đã được sử dụng lại. Phiên hiện tại đã bị thu hồi.",
                StatusCodes.Status401Unauthorized);
        }

        if (refreshToken.ExpiresAt <= DateTime.UtcNow)
        {
            refreshToken.RevokedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            await TryRemoveCacheAsync(oldCacheKey);
            throw new AuthException(
                AuthErrorCodes.RefreshTokenExpired,
                "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
                StatusCodes.Status401Unauthorized);
        }

        if (!refreshToken.User.IsActive)
        {
            await RevokeSessionTokensAsync(refreshToken.SessionId);
            await transaction.CommitAsync();
            await TryRemoveCacheAsync(oldCacheKey);
            throw CreateInactiveAccountException();
        }

        var roles = await _userManager.GetRolesAsync(refreshToken.User);
        var accessToken = await _jwtTokenService.GenerateAccessTokenAsync(
            refreshToken.User,
            roles);
        var newRawRefreshToken = _jwtTokenService.GenerateRawRefreshToken();
        var newRefreshTokenHash = _jwtTokenService.HashToken(newRawRefreshToken);

        refreshToken.RevokedAt = DateTime.UtcNow;
        refreshToken.ReplacedByTokenHash = newRefreshTokenHash;

        var newRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = refreshToken.UserId,
            SessionId = refreshToken.SessionId,
            TokenHash = newRefreshTokenHash,
            JwtId = accessToken.JwtId,
            DeviceId = NormalizeDeviceMetadata(request.DeviceId) ?? refreshToken.DeviceId,
            DeviceName = refreshToken.DeviceName,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays)
        };

        _dbContext.RefreshTokens.Add(newRefreshToken);
        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        await TryRemoveCacheAsync(oldCacheKey);
        await TryCacheRefreshTokenAsync(newRefreshToken);
        return CreateResponse(refreshToken.User, roles, accessToken, newRawRefreshToken);
    }

    public async Task LogoutAsync(LogoutRequest request)
    {
        var tokenHash = _jwtTokenService.HashToken(request.RefreshToken);
        var refreshToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash);

        if (refreshToken == null)
        {
            return;
        }

        await RevokeSessionTokensAsync(refreshToken.SessionId);
        await TryRemoveCacheAsync(
            RedisKeys.RefreshToken(Convert.ToHexString(tokenHash)));
    }

    private async Task<(AspNetUser User, bool IsNewUser)> FindOrCreatePhoneUserAsync(
        string phoneNumber)
    {
        var users = await _userManager.Users
            .Where(x => x.PhoneNumber == phoneNumber)
            .Take(2)
            .ToListAsync();

        if (users.Count > 1)
        {
            throw new AuthException(
                AuthErrorCodes.AccountConflict,
                "Số điện thoại đang thuộc nhiều tài khoản.",
                StatusCodes.Status409Conflict);
        }

        if (users.Count == 1)
        {
            var existing = users[0];
            EnsureUserActive(existing);
            if (!existing.PhoneNumberConfirmed)
            {
                existing.PhoneNumberConfirmed = true;
                EnsureIdentityResult(
                    await _userManager.UpdateAsync(existing),
                    AuthErrorCodes.AccountConflict,
                    "Không thể xác nhận số điện thoại.");
            }
            return (existing, false);
        }

        var user = new AspNetUser
        {
            Id = Guid.NewGuid(),
            UserName = phoneNumber,
            PhoneNumber = phoneNumber,
            PhoneNumberConfirmed = true,
            FullName = "Người dùng SafeRide",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        EnsureIdentityResult(
            await _userManager.CreateAsync(user),
            AuthErrorCodes.AccountConflict,
            "Không thể tạo tài khoản.");
        return (user, true);
    }

    private async Task<AspNetUser> FindOrCreateGoogleUserAsync(GoogleUserInfo googleUser)
    {
        var normalizedEmail = _userManager.NormalizeEmail(googleUser.Email);
        var users = await _userManager.Users
            .Where(x => x.NormalizedEmail == normalizedEmail)
            .Take(2)
            .ToListAsync();

        if (users.Count > 1)
        {
            throw new AuthException(
                AuthErrorCodes.AccountConflict,
                "Email Google đang thuộc nhiều tài khoản.",
                StatusCodes.Status409Conflict);
        }

        if (users.Count == 1)
        {
            var existing = users[0];
            EnsureUserActive(existing);
            existing.EmailConfirmed = true;
            existing.FullName ??= googleUser.Name;
            existing.AvatarUrl ??= googleUser.Picture;
            existing.UpdatedAt = DateTime.UtcNow;
            EnsureIdentityResult(
                await _userManager.UpdateAsync(existing),
                AuthErrorCodes.AccountConflict,
                "Không thể cập nhật tài khoản Google.");
            return existing;
        }

        var user = new AspNetUser
        {
            Id = Guid.NewGuid(),
            UserName = $"google_{googleUser.Subject}",
            Email = googleUser.Email,
            EmailConfirmed = true,
            FullName = googleUser.Name ?? googleUser.Email,
            AvatarUrl = googleUser.Picture,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        EnsureIdentityResult(
            await _userManager.CreateAsync(user),
            AuthErrorCodes.AccountConflict,
            "Không thể tạo tài khoản Google.");
        return user;
    }

    private async Task EnsurePhoneLoginAsync(AspNetUser user, string phoneNumber)
    {
        var linkedUser = await _userManager.FindByLoginAsync(PhoneLoginProvider, phoneNumber);
        if (linkedUser != null && linkedUser.Id != user.Id)
        {
            throw new AuthException(
                AuthErrorCodes.AccountConflict,
                "Số điện thoại đã liên kết với tài khoản khác.",
                StatusCodes.Status409Conflict);
        }

        if (linkedUser == null)
        {
            EnsureIdentityResult(
                await _userManager.AddLoginAsync(
                    user,
                    new UserLoginInfo(PhoneLoginProvider, phoneNumber, PhoneLoginProvider)),
                AuthErrorCodes.AccountConflict,
                "Không thể liên kết số điện thoại.");
        }
    }

    private async Task EnsureGoogleLoginAsync(AspNetUser user, string subject)
    {
        var linkedUser = await _userManager.FindByLoginAsync(GoogleLoginProvider, subject);
        if (linkedUser != null && linkedUser.Id != user.Id)
        {
            throw new AuthException(
                AuthErrorCodes.AccountConflict,
                "Tài khoản Google đã liên kết với người dùng khác.",
                StatusCodes.Status409Conflict);
        }

        if (linkedUser == null)
        {
            EnsureIdentityResult(
                await _userManager.AddLoginAsync(
                    user,
                    new UserLoginInfo(
                        GoogleLoginProvider,
                        subject,
                        GoogleLoginProvider)),
                AuthErrorCodes.AccountConflict,
                "Không thể liên kết tài khoản Google.");
        }
    }

    private async Task EnsureCustomerRoleAsync(AspNetUser user)
    {
        if (!await _userManager.IsInRoleAsync(user, CustomerRole))
        {
            EnsureIdentityResult(
                await _userManager.AddToRoleAsync(user, CustomerRole),
                AuthErrorCodes.ConfigurationUnavailable,
                "Không thể gán quyền mặc định.");
        }
    }

    private static void EnsureUserActive(AspNetUser user)
    {
        if (!user.IsActive)
        {
            throw CreateInactiveAccountException();
        }
    }

    private static AuthException CreateInactiveAccountException()
    {
        return new AuthException(
            AuthErrorCodes.AccountInactive,
            "Tài khoản đã bị vô hiệu hóa.",
            StatusCodes.Status403Forbidden);
    }

    private static void EnsureIdentityResult(
        IdentityResult result,
        string errorCode,
        string message)
    {
        if (!result.Succeeded)
        {
            var details = string.Join("; ", result.Errors.Select(x => x.Description));
            throw new AuthException(
                errorCode,
                string.IsNullOrWhiteSpace(details) ? message : $"{message} {details}",
                StatusCodes.Status409Conflict);
        }
    }

    private async Task<AuthResponse> GenerateTokenResponseAsync(
        AspNetUser user,
        string? deviceId,
        string? deviceName,
        bool isNewUser = false)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var hasDriverRegistration =
            roles.Any(role => role.Equals("Driver", StringComparison.OrdinalIgnoreCase)) ||
            await _dbContext.DriverProfiles.AnyAsync(x => x.DriverId == user.Id) ||
            await _dbContext.DriverKycs.AnyAsync(x => x.DriverId == user.Id);
        var profileIncomplete =
            string.IsNullOrWhiteSpace(user.FullName) ||
            user.FullName == "Người dùng SafeRide";
        var nextStep = hasDriverRegistration
            ? AuthNextSteps.SelectRole
            : isNewUser || profileIncomplete
                ? AuthNextSteps.CompleteProfile
                : AuthNextSteps.CustomerHome;
        var accessToken = await _jwtTokenService.GenerateAccessTokenAsync(user, roles);
        var rawRefreshToken = _jwtTokenService.GenerateRawRefreshToken();
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SessionId = Guid.NewGuid(),
            TokenHash = _jwtTokenService.HashToken(rawRefreshToken),
            JwtId = accessToken.JwtId,
            DeviceId = NormalizeDeviceMetadata(deviceId),
            DeviceName = NormalizeDeviceMetadata(deviceName),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays)
        };

        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync();
        await TryCacheRefreshTokenAsync(refreshToken);
        return CreateResponse(user, roles, accessToken, rawRefreshToken, nextStep);
    }

    private async Task RevokeSessionTokensAsync(Guid sessionId)
    {
        var activeTokens = await _dbContext.RefreshTokens
            .Where(x => x.SessionId == sessionId && x.RevokedAt == null)
            .ToListAsync();
        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
        }
        await _dbContext.SaveChangesAsync();
        foreach (var token in activeTokens)
        {
            await TryRemoveCacheAsync(
                RedisKeys.RefreshToken(Convert.ToHexString(token.TokenHash)));
        }
    }

    private async Task TryCacheRefreshTokenAsync(RefreshToken token)
    {
        try
        {
            await _redisService.SetAsync(
                RedisKeys.RefreshToken(Convert.ToHexString(token.TokenHash)),
                JsonSerializer.Serialize(new
                {
                    token.UserId,
                    token.SessionId,
                    token.ExpiresAt,
                    token.DeviceId
                }),
                token.ExpiresAt - DateTime.UtcNow);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Redis unavailable while caching refresh session {SessionId}.",
                token.SessionId);
        }
    }

    private async Task TryRemoveCacheAsync(string key)
    {
        try
        {
            await _redisService.RemoveAsync(key);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Redis unavailable while removing auth cache.");
        }
    }

    private string ComputeOtpHash(string phoneNumber, string otpCode)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
        return Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes($"{phoneNumber}:{otpCode}")));
    }

    private static string NormalizePhoneNumber(string phoneNumber)
    {
        var digits = new string((phoneNumber ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length < 9 || digits.Length > 15)
        {
            return string.Empty;
        }
        if (digits.StartsWith("84"))
        {
            return $"+{digits}";
        }
        return digits.StartsWith('0') ? $"+84{digits[1..]}" : $"+{digits}";
    }

    private static string? NormalizeDeviceMetadata(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static AuthResponse CreateResponse(
        AspNetUser user,
        IList<string> roles,
        (string Token, string JwtId, int ExpiresIn) accessToken,
        string refreshToken,
        string nextStep = AuthNextSteps.CustomerHome)
    {
        return new AuthResponse
        {
            AccessToken = accessToken.Token,
            RefreshToken = refreshToken,
            ExpiresIn = accessToken.ExpiresIn,
            UserId = user.Id,
            FullName = user.FullName ?? user.UserName ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            Roles = roles,
            NextStep = nextStep
        };
    }
}
