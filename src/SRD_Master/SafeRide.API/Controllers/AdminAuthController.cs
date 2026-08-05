using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Features.Auth.DTOs;
using SafeRide.Application.Features.Auth.Services;
using SafeRide.Domain.Entities;

namespace SafeRide.API.Controllers;

[ApiController]
[Route("api/admin/auth")]
public sealed class AdminAuthController : ControllerBase
{
    private readonly UserManager<AspNetUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;

    public AdminAuthController(
        UserManager<AspNetUser> userManager,
        IJwtTokenService jwtTokenService)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] AdminLoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive ||
            !await _userManager.CheckPasswordAsync(user, request.Password) ||
            !await _userManager.IsInRoleAsync(user, "Admin"))
        {
            return Unauthorized(new { message = "Email hoặc mật khẩu quản trị không đúng." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = await _jwtTokenService.GenerateAccessTokenAsync(user, roles);
        var refreshToken = await _jwtTokenService.GenerateRefreshTokenAsync(
            user.Id, request.DeviceId, request.DeviceName ?? "SafeRide Admin Web");

        return Ok(new AuthResponse
        {
            AccessToken = accessToken.Token,
            RefreshToken = refreshToken,
            ExpiresIn = accessToken.ExpiresIn,
            UserId = user.Id,
            FullName = user.FullName ?? user.Email ?? "Administrator",
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            Roles = roles,
            NextStep = "adminHome"
        });
    }
}

public sealed class AdminLoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
}
