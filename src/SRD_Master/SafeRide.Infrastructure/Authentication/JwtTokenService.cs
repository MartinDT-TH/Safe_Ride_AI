using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SafeRide.Application.Features.Auth.Services;
using SafeRide.Domain.Entities;
using SafeRide.Infrastructure.Persistence;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SafeRide.Infrastructure.Authentication;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly UserManager<AspNetUser> _userManager;
    private readonly ApplicationDbContext _dbContext;

    public JwtTokenService(
        IConfiguration configuration,
        UserManager<AspNetUser> userManager,
        ApplicationDbContext dbContext)
    {
        _configuration = configuration;
        _userManager = userManager;
        _dbContext = dbContext;
    }

    public async Task<(string Token, string JwtId, int ExpiresIn)> GenerateAccessTokenAsync(
        AspNetUser user,
        IList<string> roles)
    {
        var jwtId = Guid.NewGuid().ToString();
        var expiresInMinutes = int.Parse(_configuration["Jwt:AccessTokenMinutes"] ?? "15");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, jwtId),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.MobilePhone, user.PhoneNumber ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.FullName)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var secretKey = _configuration["Jwt:SecretKey"]!;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
            signingCredentials: credentials);

        return (
            new JwtSecurityTokenHandler().WriteToken(token),
            jwtId,
            expiresInMinutes * 60
        );
    }

    public string GenerateRawRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    public async Task<string> GenerateRefreshTokenAsync(Guid userId, string? deviceId, string? deviceName)
    {
        var rawToken = GenerateRawRefreshToken();
        var tokenHash = 
            SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            DeviceId = deviceId,
            DeviceName = deviceName,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
            //IsRevoked = false
        };

        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync();

        return rawToken;
    }

    public byte[] HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return bytes;
        //return Convert.ToBase64String(bytes);
    }
}