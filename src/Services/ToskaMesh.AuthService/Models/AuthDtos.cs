namespace ToskaMesh.AuthService.Models;

public record RegisterRequest(
    string Email,
    string Password,
    string FullName,
    string? PhoneNumber,
    string[] Roles);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    string UserId,
    string Email,
    IEnumerable<string> Roles);

public record LoginRequest(string Email, string Password, string? ClientId, string? IpAddress);
public record RefreshTokenRequest(string RefreshToken);
public record PasswordResetRequest(string Email);
public record PasswordResetConfirmRequest(string Email, string Token, string NewPassword);
public record EmailVerificationRequest(string Email);
public record EmailVerificationConfirmRequest(string Email, string Token);
public record ExternalLoginRequest(string Provider, string ExternalUserId, string Email, string DisplayName);
public record UpdateProfileRequest(string FullName, string? PhoneNumber);
