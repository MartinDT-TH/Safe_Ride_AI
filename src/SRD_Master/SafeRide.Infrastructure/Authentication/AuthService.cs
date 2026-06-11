using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Features.Auth.DTOs;
using SafeRide.Application.Features.Auth.Services;
using SafeRide.Domain.Entities;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;
using System.Text.Json;

namespace SafeRide.Infrastructure.Authentication;

public class AuthService : IAuthService
{
    private readonly UserManager<AspNetUser> _userManager;
    private readonly SignInManager<AspNetUser> _signInManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly IFirebaseTokenVerifier _firebaseTokenVerifier;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRedisService _redisService;
    private readonly ISpeedSmsService _speedSmsService;

    public AuthService(
        UserManager<AspNetUser> userManager,
        SignInManager<AspNetUser> signInManager,
        ApplicationDbContext dbContext,
        IFirebaseTokenVerifier firebaseTokenVerifier,
        IJwtTokenService jwtTokenService,
        IRedisService redisService,
        ISpeedSmsService speedSmsService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _dbContext = dbContext;
        _firebaseTokenVerifier = firebaseTokenVerifier;
        _jwtTokenService = jwtTokenService;
        _redisService = redisService;
        _speedSmsService = speedSmsService;
    }

    public async Task SendOtpAsync(SendOtpRequest request)
    {
        var phoneNumber = NormalizePhoneNumber(request.PhoneNumber);

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new Exception("Số điện thoại không hợp lệ.");
        }

        var otpCode = Random.Shared.Next(100000, 999999).ToString();

        await _redisService.SetAsync(
            RedisKeys.Otp(phoneNumber),
            otpCode,
            TimeSpan.FromMinutes(5));

        await _speedSmsService.SendOtpAsync(phoneNumber, otpCode);
    }

    private static string NormalizePhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return string.Empty;
        }

        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());

        if (string.IsNullOrWhiteSpace(digits))
        {
            return string.Empty;
        }

        if (digits.StartsWith("84"))
        {
            return "+" + digits;
        }

        if (digits.StartsWith("0"))
        {
            return "+84" + digits[1..];
        }

        return "+" + digits;
    }

    public async Task<AuthResponse> FirebaseLoginAsync(
        FirebaseLoginRequest request,
        string? ipAddress,
        string? userAgent)
    {
        var firebaseUser = await _firebaseTokenVerifier.VerifyAsync(request.FirebaseIdToken);

        AspNetUser? user = null;

        if (!string.IsNullOrWhiteSpace(firebaseUser.Email))
        {
            user = await _userManager.FindByEmailAsync(firebaseUser.Email);
        }

        if (user == null && !string.IsNullOrWhiteSpace(firebaseUser.PhoneNumber))
        {
            user = await _userManager.Users
                .FirstOrDefaultAsync(x => x.PhoneNumber == firebaseUser.PhoneNumber);
        }

        if (user == null)
        {
            user = new AspNetUser
            {
                Id = Guid.NewGuid(),
                UserName = firebaseUser.Email ?? firebaseUser.PhoneNumber ?? firebaseUser.FirebaseUid,
                Email = firebaseUser.Email,
                PhoneNumber = firebaseUser.PhoneNumber,
                FullName = firebaseUser.Name ?? "Người dùng SafeRide",
                AvatarUrl = firebaseUser.Picture,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user);

            if (!createResult.Succeeded)
            {
                throw new Exception("Không thể tạo tài khoản người dùng");
            }

            await _userManager.AddToRoleAsync(user, "Customer");
        }

        var existingLogin = await _userManager.FindByLoginAsync(
            "Firebase",
            firebaseUser.FirebaseUid);

        if (existingLogin == null)
        {
            await _userManager.AddLoginAsync(
                user,
                new UserLoginInfo(
                    "Firebase",
                    firebaseUser.FirebaseUid,
                    "Firebase"));
        }

        return await GenerateTokenResponseAsync(
            user,
            request.DeviceId,
            request.DeviceName,
            ipAddress,
            userAgent);
    }

    public async Task<AuthResponse> RefreshTokenAsync(
        RefreshTokenRequest request,
        string? ipAddress,
        string? userAgent)
    {
        byte[] tokenHash = _jwtTokenService.HashToken(request.RefreshToken);
        string tokenHashKey = Convert.ToHexString(tokenHash);

        var redisKey = RedisKeys.RefreshToken(tokenHashKey);

        var refreshToken = await _dbContext.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash);

        if (refreshToken == null)
        {
            throw new Exception("Phiên đăng nhập không hợp lệ");
        }

        if (refreshToken.RevokedAt != null)
        {
            await RevokeUserTokensAsync(refreshToken.UserId);
            throw new Exception("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại");
        }

        if (refreshToken.ExpiresAt <= DateTime.UtcNow)
        {
            throw new Exception("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại");
        }

        refreshToken.RevokedAt = DateTime.UtcNow;

        var user = refreshToken.User;
        var roles = await _userManager.GetRolesAsync(user);

        var accessToken = await _jwtTokenService.GenerateAccessTokenAsync(user, roles);

        var newRawRefreshToken = _jwtTokenService.GenerateRawRefreshToken();
        byte[] newRefreshTokenHash = _jwtTokenService.HashToken(newRawRefreshToken);
        string newRefreshTokenHashKey = Convert.ToHexString(newRefreshTokenHash);

        refreshToken.ReplacedByTokenHash = newRefreshTokenHash;

        var newRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = newRefreshTokenHash,
            JwtId = accessToken.JwtId,
            DeviceId = request.DeviceId ?? refreshToken.DeviceId,
            DeviceName = refreshToken.DeviceName,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        _dbContext.RefreshTokens.Add(newRefreshToken);
        await _dbContext.SaveChangesAsync();

        await _redisService.RemoveAsync(redisKey);

        await _redisService.SetAsync(
            RedisKeys.RefreshToken(newRefreshTokenHashKey),
            JsonSerializer.Serialize(new
            {
                userId = user.Id,
                expiresAt = newRefreshToken.ExpiresAt,
                deviceId = newRefreshToken.DeviceId
            }),
            newRefreshToken.ExpiresAt - DateTime.UtcNow);

        return new AuthResponse
        {
            AccessToken = accessToken.Token,
            RefreshToken = newRawRefreshToken,
            ExpiresIn = accessToken.ExpiresIn,
            UserId = user.Id,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            Email = user.Email,
            Roles = roles
        };
    }

    public async Task LogoutAsync(LogoutRequest request)
    {
        byte[] tokenHash = _jwtTokenService.HashToken(request.RefreshToken);
        string tokenHashKey = Convert.ToHexString(tokenHash);

        var refreshToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash);

        if (refreshToken == null)
        {
            return;
        }

        refreshToken.RevokedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        await _redisService.RemoveAsync(
            RedisKeys.RefreshToken(tokenHashKey));
    }

    private async Task<AuthResponse> GenerateTokenResponseAsync(
        AspNetUser user,
        string? deviceId,
        string? deviceName,
        string? ipAddress,
        string? userAgent)
    {
        var roles = await _userManager.GetRolesAsync(user);

        var accessToken = await _jwtTokenService.GenerateAccessTokenAsync(user, roles);

        var rawRefreshToken = _jwtTokenService.GenerateRawRefreshToken();

        byte[] refreshTokenHash = _jwtTokenService.HashToken(rawRefreshToken);
        string refreshTokenHashKey = Convert.ToHexString(refreshTokenHash);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            JwtId = accessToken.JwtId,
            DeviceId = deviceId,
            DeviceName = deviceName,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync();

        await _redisService.SetAsync(
            RedisKeys.RefreshToken(refreshTokenHashKey),
            JsonSerializer.Serialize(new
            {
                userId = user.Id,
                expiresAt = refreshToken.ExpiresAt,
                deviceId
            }),
            refreshToken.ExpiresAt - DateTime.UtcNow);

        return new AuthResponse
        {
            AccessToken = accessToken.Token,
            RefreshToken = rawRefreshToken,
            ExpiresIn = accessToken.ExpiresIn,
            UserId = user.Id,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            Email = user.Email,
            Roles = roles
        };
    }

    private async Task RevokeUserTokensAsync(Guid userId)
    {
        var activeTokens = await _dbContext.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTime.UtcNow;

            var tokenHashKey = Convert.ToHexString(token.TokenHash);

            await _redisService.RemoveAsync(
                RedisKeys.RefreshToken(tokenHashKey));
        }

        await _dbContext.SaveChangesAsync();
    }
}