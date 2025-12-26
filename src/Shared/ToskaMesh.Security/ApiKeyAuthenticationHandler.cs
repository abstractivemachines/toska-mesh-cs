using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace ToskaMesh.Security;

/// <summary>
/// Options for API key authentication.
/// </summary>
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
    public string Scheme => DefaultScheme;
    public string HeaderName { get; set; } = "X-API-Key";
    public Dictionary<string, ApiKeyInfo> ValidApiKeys { get; set; } = new();
}

/// <summary>
/// Information about an API key.
/// </summary>
public class ApiKeyInfo
{
    public string ServiceName { get; set; } = string.Empty;
    public string ServiceId { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}

/// <summary>
/// Authentication handler for API key authentication.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check if API key header exists
        if (!Request.Headers.TryGetValue(Options.HeaderName, out var apiKeyHeaderValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var providedApiKey = apiKeyHeaderValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedApiKey))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Validate API key
        if (!Options.ValidApiKeys.TryGetValue(providedApiKey, out var apiKeyInfo))
        {
            Logger.LogWarning("Invalid API key provided: {ApiKey}",
                providedApiKey.Substring(0, Math.Min(8, providedApiKey.Length)) + "...");
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));
        }

        // Create claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, apiKeyInfo.ServiceName),
            new Claim(MeshAuthorizationPolicies.ServiceIdClaim, apiKeyInfo.ServiceId),
            new Claim(MeshAuthorizationPolicies.ApiKeyClaim, providedApiKey)
        };

        // Add roles
        claims.AddRange(apiKeyInfo.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, Options.Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Options.Scheme);

        Logger.LogInformation("API key authenticated for service: {ServiceName}", apiKeyInfo.ServiceName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers["WWW-Authenticate"] = $"{Options.Scheme} realm=\"ToskaMesh\"";
        Response.StatusCode = 401;
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 403;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Extension methods for registering API key authentication.
/// </summary>
public static class ApiKeyAuthenticationExtensions
{
    public static AuthenticationBuilder AddApiKeyAuthentication(
        this AuthenticationBuilder builder,
        Action<ApiKeyAuthenticationOptions> configureOptions)
    {
        return builder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationOptions.DefaultScheme,
            configureOptions);
    }
}
