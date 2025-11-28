using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ToskaMesh.AuthService.Data;
using ToskaMesh.AuthService.Entities;
using ToskaMesh.AuthService.Models;
using ToskaMesh.AuthService.Services;
using ToskaMesh.Security;

namespace ToskaMesh.AuthService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<MeshUser> _userManager;
    private readonly SignInManager<MeshUser> _signInManager;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IEmailSender _emailSender;
    private readonly IAuditService _auditService;
    private readonly AuthDbContext _dbContext;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<MeshUser> userManager,
        SignInManager<MeshUser> signInManager,
        IRefreshTokenService refreshTokenService,
        JwtTokenService jwtTokenService,
        IEmailSender emailSender,
        IAuditService auditService,
        AuthDbContext dbContext,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _refreshTokenService = refreshTokenService;
        _jwtTokenService = jwtTokenService;
        _emailSender = emailSender;
        _auditService = auditService;
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null)
        {
            return Conflict("Email already registered");
        }

        var user = new MeshUser
        {
            UserName = request.Email,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            FullName = request.FullName,
            EmailConfirmed = false
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        if (request.Roles?.Length > 0)
        {
            var roles = request.Roles.Select(role => role.Trim()).Where(role => !string.IsNullOrWhiteSpace(role));
            foreach (var role in roles)
            {
                if (!await _userManager.IsInRoleAsync(user, role))
                {
                    await _userManager.AddToRoleAsync(user, role);
                }
            }
        }

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        await _emailSender.SendAsync(user.Email!, "Verify your email", $"Token: {token}");
        await _auditService.RecordAsync(user.Id, "register", new { user.Email }, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);

        return Accepted(new { Message = "Registration successful. Please verify your email." });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);
        if (user == null)
        {
            return Unauthorized("Invalid credentials");
        }

        if (!user.IsActive)
        {
            return Forbid();
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
        if (!result.Succeeded)
        {
            return Unauthorized("Invalid credentials");
        }

        var token = await IssueTokensAsync(user, request.ClientId, request.IpAddress, cancellationToken);
        await _auditService.RecordAsync(user.Id, "login", new { request.ClientId }, request.IpAddress, cancellationToken);
        return Ok(token);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var refreshToken = await _refreshTokenService.ValidateAsync(request.RefreshToken, cancellationToken);
        if (refreshToken == null)
        {
            return Unauthorized("Invalid refresh token");
        }

        var user = refreshToken.User;
        var response = await IssueTokensAsync(user, refreshToken.ClientId, refreshToken.IpAddress, cancellationToken);
        await _refreshTokenService.RevokeAsync(refreshToken, cancellationToken);
        return Ok(response);
    }

    [HttpPost("password/reset")]
    [AllowAnonymous]
    public async Task<IActionResult> RequestPasswordReset(PasswordResetRequest request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return Ok();
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        await _emailSender.SendAsync(user.Email!, "Password reset", $"Token: {token}");
        await _auditService.RecordAsync(user.Id, "password_reset_requested", null, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return Ok();
    }

    [HttpPost("password/reset/confirm")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmPasswordReset(PasswordResetConfirmRequest request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return NotFound();
        }

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        await _auditService.RecordAsync(user.Id, "password_reset", null, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return Ok();
    }

    [HttpPost("email/verify")]
    [AllowAnonymous]
    public async Task<IActionResult> SendEmailVerification(EmailVerificationRequest request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return NotFound();
        }

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        await _emailSender.SendAsync(user.Email!, "Verify your email", $"Token: {token}");
        return Ok();
    }

    [HttpPost("email/confirm")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(EmailVerificationConfirmRequest request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return NotFound();
        }

        var result = await _userManager.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        await _auditService.RecordAsync(user.Id, "email_confirmed", null, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return Ok();
    }

    [HttpPost("external")]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalLogin(ExternalLoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            user = new MeshUser
            {
                UserName = request.Email,
                Email = request.Email,
                EmailConfirmed = true,
                FullName = request.DisplayName
            };
            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                return BadRequest(createResult.Errors);
            }
        }

        var loginInfo = new UserLoginInfo(request.Provider, request.ExternalUserId, request.Provider);
        var existing = await _userManager.FindByLoginAsync(request.Provider, request.ExternalUserId);
        if (existing == null)
        {
            var addLogin = await _userManager.AddLoginAsync(user, loginInfo);
            if (!addLogin.Succeeded)
            {
                return BadRequest(addLogin.Errors);
            }
        }

        var tokens = await IssueTokensAsync(user, request.Provider, null, cancellationToken);
        await _auditService.RecordAsync(user.Id, "external_login", new { request.Provider }, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return Ok(tokens);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        var users = await _userManager.Users
            .Select(user => new
            {
                user.Id,
                user.Email,
                user.FullName,
                user.IsActive,
                user.EmailConfirmed,
                user.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new
        {
            user.Id,
            user.Email,
            user.FullName,
            user.PhoneNumber,
            Roles = roles
        });
    }

    [Authorize]
    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        user.FullName = request.FullName;
        user.PhoneNumber = request.PhoneNumber;
        await _userManager.UpdateAsync(user);
        await _auditService.RecordAsync(user.Id, "profile_updated", null, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return NoContent();
    }

    private async Task<AuthResponse> IssueTokensAsync(MeshUser user, string? clientId, string? ipAddress, CancellationToken cancellationToken)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _jwtTokenService.GenerateToken(user.Id, user.Email!, roles);
        var refreshToken = await _refreshTokenService.IssueAsync(user, clientId, ipAddress, cancellationToken);
        var refreshTokenValue = refreshToken.PlaintextToken ?? throw new InvalidOperationException("Failed to generate refresh token value.");
        return new AuthResponse(accessToken, refreshTokenValue, DateTime.UtcNow.AddHours(1), user.Id, user.Email!, roles);
    }
}
