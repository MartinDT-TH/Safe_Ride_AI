using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Features.Auth.DTOs;
using SafeRide.Application.Features.Auth.Services;
using SafeRide.Domain.Entities;
using SafeRide.Infrastructure.Authentication;

namespace SafeRide.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly UserManager<AspNetUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IHostEnvironment _environment;

    public AuthController(
        IAuthService authService,
        UserManager<AspNetUser> userManager,
        IJwtTokenService jwtTokenService,
        IHostEnvironment environment)
    {
        _authService = authService;
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _environment = environment;
    }



    [HttpPost("demo-login")]
    public async Task<IActionResult> DemoLogin([FromBody] DemoLoginRequest request)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        if (request.Provider != "Phone" && request.Provider != "Google")
        {
            return BadRequest(new
            {
                message = "Provider chỉ được là Phone hoặc Google."
            });
        }

        if (request.Provider == "Phone" && string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return BadRequest(new
            {
                message = "Số điện thoại không được để trống."
            });
        }

        if (request.Provider == "Google" && string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new
            {
                message = "Email không được để trống."
            });
        }

        AspNetUser? user = null;

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            user = await _userManager.Users
                .FirstOrDefaultAsync(x => x.PhoneNumber == request.PhoneNumber);
        }

        if (user == null && !string.IsNullOrWhiteSpace(request.Email))
        {
            user = await _userManager.FindByEmailAsync(request.Email);
        }

        if (user == null)
        {
            user = new AspNetUser
            {
                Id = Guid.NewGuid(),
                UserName = request.PhoneNumber ?? request.Email,
                PhoneNumber = request.PhoneNumber,
                Email = request.Email,
                FullName = request.FullName,
                AvatarUrl = request.AvatarUrl,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user);

            if (!createResult.Succeeded)
            {
                return BadRequest(new
                {
                    message = "Không thể tạo tài khoản demo.",
                    errors = createResult.Errors.Select(e => e.Description)
                });
            }

            var roleResult = await _userManager.AddToRoleAsync(user, "Customer");
            if (!roleResult.Succeeded)
            {
                return Conflict(new
                {
                    code = "auth.role_assignment_failed",
                    errors = roleResult.Errors.Select(e => e.Description)
                });
            }
        }

        var demoProviderKey = request.Provider == "Phone"
            ? $"demo-phone:{request.PhoneNumber}"
            : $"demo-google:{request.Email}";

        var loginProvider = request.Provider == "Phone"
            ? "DemoPhone"
            : "DemoGoogle";

        var existingLogins = await _userManager.GetLoginsAsync(user);

        var alreadyLinked = existingLogins.Any(x =>
            x.LoginProvider == loginProvider &&
            x.ProviderKey == demoProviderKey);

        if (!alreadyLinked)
        {
            var loginResult = await _userManager.AddLoginAsync(
                user,
                new UserLoginInfo(
                    loginProvider,
                    demoProviderKey,
                    loginProvider));

            if (!loginResult.Succeeded)
            {
                return Conflict(new
                {
                    code = "auth.account_conflict",
                    errors = loginResult.Errors.Select(e => e.Description)
                });
            }
        }
        var roles = await _userManager.GetRolesAsync(user);

        var accessToken = await _jwtTokenService.GenerateAccessTokenAsync(user, roles);

        var refreshToken = await _jwtTokenService.GenerateRefreshTokenAsync(
            user.Id,
            request.DeviceId,
            request.DeviceName
        );

        return Ok(new AuthResponse
        {
            AccessToken = accessToken.Token,
            RefreshToken = refreshToken,
            ExpiresIn = accessToken.ExpiresIn,
            UserId = user.Id,
            FullName = user.FullName ?? user.UserName ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            Email = user.Email,
            Roles = roles
        });
    }

    [HttpPost("send-otp")]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request)
    {
        await _authService.SendOtpAsync(request);

        return Ok(new
        {
            message = "Mã OTP đã được gửi thành công."
        });
    }

    [HttpPost("google-login")]
    public async Task<IActionResult> GoogleLogin(
        [FromBody] GoogleLoginRequest request)
    {
        var response = await _authService.GoogleLoginAsync(
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());

        return Ok(response);
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        var response = await _authService.VerifyOtpAsync(
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());

        return Ok(response);
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken(
        [FromBody] RefreshTokenRequest request)
    {
        var response = await _authService.RefreshTokenAsync(
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());

        return Ok(response);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request)
    {
        await _authService.LogoutAsync(request);

        return Ok(new
        {
            message = "Đăng xuất thành công"
        });
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            message = "Token hợp lệ"
        });
    }
}
