using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Features.Auth.DTOs;
using SafeRide.Application.Features.Auth.Services;
using SafeRide.Application.Features.Auth;
using SafeRide.Domain.Entities;
using SafeRide.Infrastructure.Authentication;
using System.Security.Claims;
using SafeRide.Infrastructure.ExternalServices.Cloudinary;

namespace SafeRide.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const string GoogleLoginProvider = "Google";

    private readonly IAuthService _authService;
    private readonly UserManager<AspNetUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ICloudinaryImageService _cloudinaryImageService;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;

    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        UserManager<AspNetUser> userManager,
        IJwtTokenService jwtTokenService,
        ICloudinaryImageService cloudinaryImageService,
        IHostEnvironment environment,
        IConfiguration configuration,
        IWebHostEnvironment env,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _cloudinaryImageService = cloudinaryImageService;
        _configuration = configuration;
        _environment = environment;
        _env = env;
        _logger = logger;
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
            PhoneNumberConfirmed = user.PhoneNumberConfirmed,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
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
        _logger.LogInformation(
            "GoogleLogin Debug: HasIdToken={HasIdToken}, IdTokenLength={IdTokenLength}, HasGoogleClientId={HasGoogleClientId}, HasJwtKey={HasJwtKey}, Environment={Environment}",
            !string.IsNullOrWhiteSpace(request.GoogleIdToken),
            request.GoogleIdToken?.Length ?? 0,
            !string.IsNullOrWhiteSpace(_configuration["Authentication:Google:ClientId"]),
            !string.IsNullOrWhiteSpace(_configuration["Jwt:Key"]),
            _env.EnvironmentName
        );
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
    [HttpGet("linked-accounts")]
    public async Task<IActionResult> GetLinkedAccounts()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await _authService.GetLinkedAccountsAsync(userId));
    }

    [Authorize]
    [HttpPost("linked-accounts/google")]
    public async Task<IActionResult> LinkGoogleAccount(
        [FromBody] LinkGoogleAccountRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await _authService.LinkGoogleAccountAsync(userId, request));
    }

    [Authorize]
    [HttpDelete("linked-accounts/google")]
    public async Task<IActionResult> UnlinkGoogleAccount()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await _authService.UnlinkGoogleAccountAsync(userId));
    }

    [Authorize]
    [HttpPost("profile/phone/send-otp")]
    public async Task<IActionResult> SendProfilePhoneOtp([FromBody] SendOtpRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        await _authService.SendProfilePhoneOtpAsync(userId, request);

        return Ok(new
        {
            message = "Mã OTP đã được gửi thành công."
        });
    }

    [Authorize]
    [HttpPost("profile/phone/verify-otp")]
    public async Task<IActionResult> VerifyProfilePhoneOtp([FromBody] VerifyOtpRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        await _authService.VerifyProfilePhoneOtpAsync(userId, request);

        return Ok(new
        {
            phoneNumber = NormalizePhoneNumber(request.PhoneNumber),
            phoneNumberConfirmed = true
        });
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return NotFound();
        }

        var requestedEmail = string.IsNullOrWhiteSpace(request.Email)
            ? null
            : request.Email.Trim();
        var normalizedRequestedEmail = requestedEmail == null
            ? null
            : _userManager.NormalizeEmail(requestedEmail);
        var emailChanged = !string.Equals(
            user.NormalizedEmail,
            normalizedRequestedEmail,
            StringComparison.OrdinalIgnoreCase);
        if (normalizedRequestedEmail != null)
        {
            var emailOwner = await _userManager.Users
                .FirstOrDefaultAsync(x =>
                    x.NormalizedEmail == normalizedRequestedEmail &&
                    x.Id != user.Id);
            if (emailOwner != null)
            {
                return Conflict(new
                {
                    code = "auth.email_conflict",
                    message = "Email đã được sử dụng bởi tài khoản khác."
                });
            }
        }

        user.FullName = request.FullName.Trim();
        if (emailChanged)
        {
            var googleLogins = (await _userManager.GetLoginsAsync(user))
                .Where(x => x.LoginProvider == GoogleLoginProvider)
                .ToList();
            foreach (var login in googleLogins)
            {
                var removeResult = await _userManager.RemoveLoginAsync(
                    user,
                    login.LoginProvider,
                    login.ProviderKey);
                if (!removeResult.Succeeded)
                {
                    return Conflict(new
                    {
                        code = "auth.google_unlink_failed",
                        errors = removeResult.Errors.Select(x => x.Description)
                    });
                }
            }

            user.EmailConfirmed = false;
        }

        user.Email = requestedEmail;
        user.NormalizedEmail = normalizedRequestedEmail;

        var requestedPhoneNumber = NormalizePhoneNumber(request.PhoneNumber);
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber) &&
            string.IsNullOrWhiteSpace(requestedPhoneNumber))
        {
            return BadRequest(new
            {
                code = "auth.invalid_phone_number",
                message = "Số điện thoại không hợp lệ."
            });
        }

        if (!string.IsNullOrWhiteSpace(requestedPhoneNumber))
        {
            var existingPhoneUser = await _userManager.Users
                .FirstOrDefaultAsync(x =>
                    x.PhoneNumber == requestedPhoneNumber &&
                    x.Id != user.Id);
            if (existingPhoneUser != null)
            {
                return Conflict(new
                {
                    code = "auth.phone_number_conflict",
                    message = "Số điện thoại đã được sử dụng bởi tài khoản khác."
                });
            }

            if (string.IsNullOrWhiteSpace(user.PhoneNumber))
            {
                return Conflict(new
                {
                    code = "auth.phone_verification_required",
                    message = "Vui lòng xác thực OTP trước khi thêm số điện thoại."
                });
            }
            else if (user.PhoneNumber != requestedPhoneNumber)
            {
                return Conflict(new
                {
                    code = "auth.phone_number_change_requires_verification",
                    message = "Không thể thay đổi số điện thoại đã liên kết tại màn hình này."
                });
            }
        }

        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return Conflict(new
            {
                code = "auth.profile_update_failed",
                errors = result.Errors.Select(x => x.Description)
            });
        }

        return Ok(new
        {
            user.Id,
            user.FullName,
            user.PhoneNumber,
            user.PhoneNumberConfirmed,
            user.Email,
            user.AvatarUrl
        });
    }

    [Authorize]
    [HttpPost("profile/avatar")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadAvatar(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        if (file.Length == 0 || file.Length > 5 * 1024 * 1024)
        {
            return BadRequest(new
            {
                code = "profile.avatar_invalid_size",
                message = "Ảnh đại diện phải có dung lượng từ 1 byte đến 5 MB."
            });
        }

        var allowedContentTypes = new[]
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };
        if (!allowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                code = "profile.avatar_invalid_type",
                message = "Ảnh đại diện chỉ hỗ trợ JPG, PNG hoặc WEBP."
            });
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return NotFound();
        }

        await using var stream = file.OpenReadStream();
        string avatarUrl;
        try
        {
            avatarUrl = await _cloudinaryImageService.UploadAvatarAsync(
                userId,
                stream,
                file.FileName,
                cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                code = "profile.avatar_upload_unavailable",
                message = exception.Message
            });
        }

        user.AvatarUrl = avatarUrl;
        user.UpdatedAt = DateTime.UtcNow;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return Conflict(new
            {
                code = "profile.avatar_update_failed",
                errors = result.Errors.Select(x => x.Description)
            });
        }

        return Ok(new { avatarUrl });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var profileIncomplete =
            string.IsNullOrWhiteSpace(user.FullName) ||
            user.FullName == "Người dùng SafeRide" ||
            string.IsNullOrWhiteSpace(user.PhoneNumber) ||
            !user.PhoneNumberConfirmed;
        var nextStep = profileIncomplete
            ? AuthNextSteps.CompleteProfile
            : roles.Any(role => role.Equals("Driver", StringComparison.OrdinalIgnoreCase))
                ? AuthNextSteps.SelectRole
                : AuthNextSteps.CustomerHome;

        return Ok(new
        {
            user.Id,
            user.FullName,
            user.PhoneNumber,
            user.PhoneNumberConfirmed,
            user.Email,
            user.AvatarUrl,
            Roles = roles,
            NextStep = nextStep
        });
    }

    private static string NormalizePhoneNumber(string? phoneNumber)
    {
        return PhoneNumberNormalizer.Normalize(phoneNumber);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out userId);
    }
}
